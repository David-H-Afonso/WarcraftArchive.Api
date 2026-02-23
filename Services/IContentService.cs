using WarcraftArchive.Api.DTOs;

namespace WarcraftArchive.Api.Services;

public interface IContentService
{
    Task<List<ContentDto>> GetAllAsync(string? search);
    Task<ContentDto?> GetByIdAsync(Guid id);
    Task<ContentDto> CreateAsync(CreateContentRequest request);
    Task<ContentDto?> UpdateAsync(Guid id, UpdateContentRequest request);
    Task<bool> DeleteAsync(Guid id);
}
