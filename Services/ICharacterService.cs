using WarcraftArchive.Api.DTOs;

namespace WarcraftArchive.Api.Services;

public interface ICharacterService
{
    Task<List<CharacterDto>> GetAllAsync(Guid? ownerUserId = null);
    Task<CharacterDto?> GetByIdAsync(Guid id);
    Task<CharacterDto> CreateAsync(CreateCharacterRequest request);
    Task<CharacterDto?> UpdateAsync(Guid id, UpdateCharacterRequest request);
    Task<bool> DeleteAsync(Guid id);
}
