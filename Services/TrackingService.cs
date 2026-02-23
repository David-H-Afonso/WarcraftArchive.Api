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
        Guid? characterId, TrackingStatus? status, Frequency? frequency,
        string? expansion, MotiveFlags? motive)
    {
        var query = _context.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content)
            .AsQueryable();

        if (characterId.HasValue)
            query = query.Where(t => t.CharacterId == characterId.Value);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (frequency.HasValue)
            query = query.Where(t => t.Frequency == frequency.Value);

        if (!string.IsNullOrWhiteSpace(expansion))
        {
            var exp = expansion.Trim().ToLower();
            query = query.Where(t => t.Content.Expansion.ToLower().Contains(exp));
        }

        if (motive.HasValue && motive.Value != MotiveFlags.None)
        {
            var motiveInt = (int)motive.Value;
            // Bitmask: any tracking whose content has at least one matching motive flag
            query = query.Where(t => (t.Content.Motives & motiveInt) != 0);
        }

        return await query
            .OrderBy(t => t.Content.Expansion)
            .ThenBy(t => t.Content.Name)
            .ThenBy(t => t.Character.Name)
            .Select(t => ToDto(t))
            .ToListAsync();
    }

    public async Task<TrackingDto?> GetByIdAsync(Guid id)
    {
        var t = await _context.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content)
            .FirstOrDefaultAsync(t => t.Id == id);
        return t == null ? null : ToDto(t);
    }

    public async Task<(TrackingDto? Dto, string? Error)> CreateAsync(CreateTrackingRequest request)
    {
        // Validate character + content exist
        var character = await _context.Characters.FindAsync(request.CharacterId);
        if (character == null)
            return (null, "Character not found.");

        var content = await _context.Contents.FindAsync(request.ContentId);
        if (content == null)
            return (null, "Content not found.");

        // Validate difficulty is allowed for this content
        var diffFlag = DifficultyToFlag(request.Difficulty);
        if ((content.AllowedDifficulties & (int)diffFlag) == 0)
            return (null,
                $"Difficulty '{request.Difficulty}' is not allowed for this content. " +
                $"Allowed: {(DifficultyFlags)content.AllowedDifficulties}");

        // Duplicate check
        var exists = await _context.Trackings.AnyAsync(t =>
            t.CharacterId == request.CharacterId &&
            t.ContentId == request.ContentId &&
            t.Difficulty == request.Difficulty);
        if (exists)
            return (null, "A tracking entry for this character, content and difficulty already exists.");

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
        await _context.Entry(tracking).Reference(t => t.Content).LoadAsync();
        return (ToDto(tracking), null);
    }

    public async Task<(TrackingDto? Dto, string? Error)> UpdateAsync(Guid id, UpdateTrackingRequest request)
    {
        var tracking = await _context.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tracking == null)
            return (null, null);

        // Validate difficulty is allowed
        var diffFlag = DifficultyToFlag(request.Difficulty);
        if ((tracking.Content.AllowedDifficulties & (int)diffFlag) == 0)
            return (null,
                $"Difficulty '{request.Difficulty}' is not allowed for this content. " +
                $"Allowed: {(DifficultyFlags)tracking.Content.AllowedDifficulties}");

        // Check uniqueness if difficulty changed
        if (request.Difficulty != tracking.Difficulty)
        {
            var exists = await _context.Trackings.AnyAsync(t =>
                t.CharacterId == tracking.CharacterId &&
                t.ContentId == tracking.ContentId &&
                t.Difficulty == request.Difficulty &&
                t.Id != id);
            if (exists)
                return (null, "A tracking entry for this character, content and difficulty already exists.");
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
        if (tracking == null)
            return false;
        _context.Trackings.Remove(tracking);
        await _context.SaveChangesAsync();
        return true;
    }

    internal static TrackingDto ToDto(Tracking t) => new(
        t.Id,
        t.CharacterId, t.Character.Name,
        t.ContentId, t.Content.Name, t.Content.Expansion,
        t.Difficulty, t.Frequency, t.Status,
        t.Comment, t.LastCompletedAt,
        t.CreatedAt, t.UpdatedAt);

    internal static DifficultyFlags DifficultyToFlag(Difficulty d) => d switch
    {
        Difficulty.LFR    => DifficultyFlags.LFR,
        Difficulty.Normal => DifficultyFlags.Normal,
        Difficulty.Heroic => DifficultyFlags.Heroic,
        Difficulty.Mythic => DifficultyFlags.Mythic,
        _                 => DifficultyFlags.None,
    };
}
