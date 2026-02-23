using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Services;

public interface ITrackingService
{
    Task<List<TrackingDto>> GetAllAsync(
        Guid? characterId, TrackingStatus? status, Frequency? frequency,
        string? expansion, MotiveFlags? motive);

    Task<TrackingDto?> GetByIdAsync(Guid id);
    Task<(TrackingDto? Dto, string? Error)> CreateAsync(CreateTrackingRequest request);
    Task<(TrackingDto? Dto, string? Error)> UpdateAsync(Guid id, UpdateTrackingRequest request);
    Task<bool> DeleteAsync(Guid id);
}
