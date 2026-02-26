using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Services;

public interface ITrackingService
{
    Task<List<TrackingDto>> GetAllAsync(
        Guid? ownerUserId, Guid? characterId, TrackingStatus? status, Frequency? frequency,
        string? expansion, Guid? motiveId, Guid? contentId);

    Task<TrackingDto?> GetByIdAsync(Guid id);
    Task<(TrackingDto? Dto, string? Error)> CreateAsync(Guid ownerUserId, CreateTrackingRequest request);
    Task<(TrackingDto? Dto, string? Error)> UpdateAsync(Guid id, Guid ownerUserId, UpdateTrackingRequest request);
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId);
}
