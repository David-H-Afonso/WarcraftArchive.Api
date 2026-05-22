namespace WarcraftArchive.Api.Application.Interfaces;

public interface IAdminService
{
    Task<object> GetOrphansAsync();
    Task<(bool Success, string? Message)> ClaimOrphanCharacterAsync(Guid characterId, Guid userId);
    Task<(bool Success, string? Message)> ClaimOrphanContentAsync(Guid contentId, Guid userId);
    Task<bool> DeleteOrphanCharacterAsync(Guid characterId);
    Task<bool> DeleteOrphanContentAsync(Guid contentId);
    Task<bool> DeleteOrphanTrackingAsync(Guid trackingId);
    Task<(int DeletedTrackings, int DeletedCharacters, int DeletedContents)> DeleteAllOrphansAsync();
}
