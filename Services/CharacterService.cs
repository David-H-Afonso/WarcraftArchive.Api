using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Services;

public class CharacterService : ICharacterService
{
    private readonly AppDbContext _context;

    public CharacterService(AppDbContext context) => _context = context;

    public async Task<List<CharacterDto>> GetAllAsync() =>
        await _context.Characters
            .Include(c => c.OwnerUser)
            .OrderBy(c => c.Name)
            .Select(c => ToDto(c))
            .ToListAsync();

    public async Task<CharacterDto?> GetByIdAsync(Guid id)
    {
        var c = await _context.Characters.Include(c => c.OwnerUser).FirstOrDefaultAsync(c => c.Id == id);
        return c == null ? null : ToDto(c);
    }

    public async Task<CharacterDto> CreateAsync(CreateCharacterRequest request)
    {
        var character = new Character
        {
            Name = request.Name.Trim(),
            Level = request.Level,
            Class = request.Class.Trim(),
            Covenant = request.Covenant?.Trim(),
            Warband = request.Warband?.Trim(),
            OwnerUserId = request.OwnerUserId,
        };
        _context.Characters.Add(character);
        await _context.SaveChangesAsync();

        // Reload with nav
        await _context.Entry(character).Reference(c => c.OwnerUser).LoadAsync();
        return ToDto(character);
    }

    public async Task<CharacterDto?> UpdateAsync(Guid id, UpdateCharacterRequest request)
    {
        var character = await _context.Characters
            .Include(c => c.OwnerUser)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (character == null)
            return null;

        character.Name = request.Name.Trim();
        character.Level = request.Level;
        character.Class = request.Class.Trim();
        character.Covenant = request.Covenant?.Trim();
        character.Warband = request.Warband?.Trim();
        character.OwnerUserId = request.OwnerUserId;

        await _context.SaveChangesAsync();

        // Reload owner if changed
        await _context.Entry(character).Reference(c => c.OwnerUser).LoadAsync();
        return ToDto(character);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var character = await _context.Characters.FindAsync(id);
        if (character == null)
            return false;
        _context.Characters.Remove(character);
        await _context.SaveChangesAsync();
        return true;
    }

    private static CharacterDto ToDto(Character c) => new(
        c.Id, c.Name, c.Level, c.Class, c.Covenant, c.Warband,
        c.OwnerUserId, c.OwnerUser?.UserName,
        c.CreatedAt, c.UpdatedAt);
}
