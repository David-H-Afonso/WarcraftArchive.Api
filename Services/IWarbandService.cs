using WarcraftArchive.Api.DTOs;

namespace WarcraftArchive.Api.Services;

public interface IWarbandService
{
    Task<List<WarbandDto>> GetAllAsync(Guid ownerUserId);
    Task<WarbandDto?> GetByIdAsync(Guid id);
    Task<(WarbandDto? Dto, string? Error)> CreateAsync(Guid ownerUserId, CreateWarbandRequest request);
    Task<WarbandDto?> UpdateAsync(Guid id, Guid ownerUserId, UpdateWarbandRequest request);
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId);
}
