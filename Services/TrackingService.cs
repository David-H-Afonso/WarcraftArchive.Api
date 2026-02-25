using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Services;

public class TrackingService : ITrackingService
{
    private readonly AppDbContext _context;
    public TrackingService(AppDbContext context) => _context = context;

    public async Task<List<TrackingDto>> GetAllAsync(
        Guid? ownerUserId, Guid? characterId, TrackingStatus? status, Frequency? frequency,
        string? expansion, Guid? motiveId, Guid? contentId)
    {
        var query = _context.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content).ThenInclude(c => c.Motives)
            .AsQueryable();

        if (ownerUserId.HasValue) query = query.Where(t => t.Character.OwnerUserId == ownerUserId.Value);
        if (characterId.HasValue) query = query.Where(t => t.CharacterId == characterId.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (frequency.HasValue) query = query.Where(t => t.Frequency == frequency.Value);
        if (contentId.HasValue) query = query.Where(t => t.ContentId == contentId.Value);
        if (!string.IsNullOrWhiteSpace(expansion))
        {
            var exp = expansion.Trim().ToLower();
            query = query.Where(t => t.Content.Expansion.ToLower().Contains(exp));
        }
        if (motiveId.HasValue)
            query = query.Where(t => t.Content.Motives.Any(m => m.Id == motiveId.Value));

        var list = await query
            .OrderBy(t => t.Content.Expansion).ThenBy(t => t.Content.Name).ThenBy(t => t.Character.Name)
            .ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<TrackingDto?> GetByIdAsync(Guid id)
    {
        var t = await _context.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content).ThenInclude(c => c.Motives)
            .FirstOrDefaultAsync(t => t.Id == id);
        return t == null ? null : ToDto(t);
    }

    public async Task<(TrackingDto? Dto, string? Error)> CreateAsync(CreateTrackingRequest request)
    {
        var character = await _context.Characters.FindAsync(request.CharacterId);
        if (character == null) return (null, "Character not found.");
        var content = await _context.Contents.FindAsync(request.ContentId);
        if (content == null) return (null, "Content not found.");
        // Difficulty is already a DifficultyFlags single-flag value
        if ((content.AllowedDifficulties & (int)request.Difficulty) == 0)
            return (null, $"Difficulty '{request.Difficulty}' is not allowed for this content. Allowed: {(DifficultyFlags)content.AllowedDifficulties}");
        var exists = await _context.Trackings.AnyAsync(t => t.CharacterId == request.CharacterId && t.ContentId == request.ContentId && t.Difficulty == request.Difficulty);
        if (exists) return (null, "A tracking entry for this character, content and difficulty already exists.");

        var tracking = new Tracking
        {
            CharacterId = request.CharacterId,
            ContentId = request.ContentId,
            Difficulty = request.Difficulty,
            Frequency = request.Frequency,
            Status = request.Status,
            Comment = request.Comment?.Trim(),
            LastCompletedAt = request.LastCompletedAt,
        };
        _context.Trackings.Add(tracking);
        await _context.SaveChangesAsync();
        await _context.Entry(tracking).Reference(t => t.Character).LoadAsync();
        await _context.Entry(tracking).Reference(t => t.Content).Query().Include(c => c.Motives).LoadAsync();
        return (ToDto(tracking), null);
    }

    public async Task<(TrackingDto? Dto, string? Error)> UpdateAsync(Guid id, UpdateTrackingRequest request)
    {
        var tracking = await _context.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content).ThenInclude(c => c.Motives)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tracking == null) return (null, null);
        // Difficulty is already a DifficultyFlags single-flag value
        if ((tracking.Content.AllowedDifficulties & (int)request.Difficulty) == 0)
            return (null, $"Difficulty '{request.Difficulty}' is not allowed for this content. Allowed: {(DifficultyFlags)tracking.Content.AllowedDifficulties}");
        if (request.Difficulty != tracking.Difficulty)
        {
            var ex2 = await _context.Trackings.AnyAsync(t => t.CharacterId == tracking.CharacterId && t.ContentId == tracking.ContentId && t.Difficulty == request.Difficulty && t.Id != id);
            if (ex2) return (null, "A tracking entry for this character, content and difficulty already exists.");
        }
        tracking.Difficulty = request.Difficulty;
        tracking.Frequency = request.Frequency;
        tracking.Status = request.Status;
        tracking.Comment = request.Comment?.Trim();
        tracking.LastCompletedAt = request.LastCompletedAt;
        await _context.SaveChangesAsync();
        return (ToDto(tracking), null);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var tracking = await _context.Trackings.FindAsync(id);
        if (tracking == null) return false;
        _context.Trackings.Remove(tracking);
        await _context.SaveChangesAsync();
        return true;
    }

    internal static TrackingDto ToDto(Tracking t) => new(
        t.Id,
        t.CharacterId, t.Character.Name, t.Character.Class, t.Character.Race,
        t.ContentId, t.Content.Name, t.Content.Expansion,
        t.Difficulty, t.Frequency, t.Status,
        t.Comment, t.LastCompletedAt,
        t.CreatedAt, t.UpdatedAt);
}
