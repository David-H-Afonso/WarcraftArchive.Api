using WarcraftArchive.Api.Contracts;

namespace WarcraftArchive.Api.Application.Interfaces;

public interface ICharacterService
{
    Task<List<CharacterDto>> GetAllAsync(Guid? ownerUserId = null);
    Task<CharacterDto?> GetByIdAsync(Guid id);
    Task<CharacterDto> CreateAsync(Guid ownerUserId, CreateCharacterRequest request);
    Task<CharacterDto?> UpdateAsync(Guid id, UpdateCharacterRequest request);
    Task<bool> DeleteAsync(Guid id);
}
