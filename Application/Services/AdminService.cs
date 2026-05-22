using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Application.Interfaces;
using WarcraftArchive.Api.Infrastructure.Persistence;

namespace WarcraftArchive.Api.Application.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;

    public AdminService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<object> GetOrphansAsync()
    {
        var characters = await _db.Characters
            .Where(c => c.OwnerUserId == null)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Class, c.Race, c.Level, c.CreatedAt })
            .ToListAsync();

        var contents = await _db.Contents
            .Where(c => c.OwnerUserId == null)
            .OrderBy(c => c.Expansion).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Expansion, c.AllowedDifficulties, c.CreatedAt })
            .ToListAsync();

        var trackings = await _db.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content)
            .Where(t => t.Character.OwnerUserId == null || t.Content.OwnerUserId == null)
            .OrderBy(t => t.Content.Name).ThenBy(t => t.Character.Name)
            .Select(t => new
            {
                t.Id,
                characterId = t.CharacterId,
                characterName = t.Character.Name,
                characterOwned = t.Character.OwnerUserId != null,
                contentId = t.ContentId,
                contentName = t.Content.Name,
                contentOwned = t.Content.OwnerUserId != null,
                t.Difficulty,
                t.CreatedAt,
            })
            .ToListAsync();

        return new { characters, contents, trackings };
    }

    public async Task<(bool Success, string? Message)> ClaimOrphanCharacterAsync(Guid characterId, Guid userId)
    {
        var character = await _db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.OwnerUserId == null);
        if (character == null) return (false, null);

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        character.OwnerUserId = userId;
        await _db.SaveChangesAsync();
        return (true, $"Character '{character.Name}' claimed by user '{user.UserName}'.");
    }

    public async Task<(bool Success, string? Message)> ClaimOrphanContentAsync(Guid contentId, Guid userId)
    {
        var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == contentId && c.OwnerUserId == null);
        if (content == null) return (false, null);

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        content.OwnerUserId = userId;
        await _db.SaveChangesAsync();
        return (true, $"Content '{content.Name}' claimed by user '{user.UserName}'.");
    }

    public async Task<bool> DeleteOrphanCharacterAsync(Guid characterId)
    {
        var character = await _db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.OwnerUserId == null);
        if (character == null) return false;
        _db.Characters.Remove(character);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteOrphanContentAsync(Guid contentId)
    {
        var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == contentId && c.OwnerUserId == null);
        if (content == null) return false;
        _db.Contents.Remove(content);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteOrphanTrackingAsync(Guid trackingId)
    {
        var tracking = await _db.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content)
            .FirstOrDefaultAsync(t => t.Id == trackingId &&
                (t.Character.OwnerUserId == null || t.Content.OwnerUserId == null));
        if (tracking == null) return false;
        _db.Trackings.Remove(tracking);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(int DeletedTrackings, int DeletedCharacters, int DeletedContents)> DeleteAllOrphansAsync()
    {
        // Delete trackings linked to orphaned characters or content first (FK)
        var orphanTrackings = await _db.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content)
            .Where(t => t.Character.OwnerUserId == null || t.Content.OwnerUserId == null)
            .ToListAsync();
        _db.Trackings.RemoveRange(orphanTrackings);

        var orphanCharacters = await _db.Characters.Where(c => c.OwnerUserId == null).ToListAsync();
        _db.Characters.RemoveRange(orphanCharacters);

        var orphanContents = await _db.Contents.Where(c => c.OwnerUserId == null).ToListAsync();
        _db.Contents.RemoveRange(orphanContents);

        await _db.SaveChangesAsync();
        return (orphanTrackings.Count, orphanCharacters.Count, orphanContents.Count);
    }
}
