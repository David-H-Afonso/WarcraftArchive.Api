using WarcraftArchive.Api.Contracts;

namespace WarcraftArchive.Api.Application.Interfaces;

public interface IContentService
{
    Task<List<ContentDto>> GetAllAsync(Guid ownerUserId, string? expansion = null);
    Task<ContentDto?> GetByIdAsync(Guid id);
    Task<ContentDto> CreateAsync(Guid ownerUserId, CreateContentRequest request);
    Task<ContentDto?> UpdateAsync(Guid id, Guid ownerUserId, UpdateContentRequest request);
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId);
}
