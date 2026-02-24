using WarcraftArchive.Api.DTOs;

namespace WarcraftArchive.Api.Services;

public interface IUserMotiveService
{
    Task<List<UserMotiveDto>> GetAllAsync(Guid ownerUserId);
    Task<UserMotiveDto?> GetByIdAsync(Guid id);
    Task<(UserMotiveDto? Dto, string? Error)> CreateAsync(Guid ownerUserId, CreateUserMotiveRequest request);
    Task<UserMotiveDto?> UpdateAsync(Guid id, Guid ownerUserId, UpdateUserMotiveRequest request);
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId);
}
