using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Auth;

namespace WarcraftArchive.Api.Services;

public class UserMotiveService : IUserMotiveService
{
    private readonly AppDbContext _context;

    public UserMotiveService(AppDbContext context) => _context = context;

    public async Task<List<UserMotiveDto>> GetAllAsync(Guid ownerUserId) =>
        await _context.UserMotives
            .Where(m => m.OwnerUserId == ownerUserId)
            .OrderBy(m => m.Name)
            .Select(m => ToDto(m))
            .ToListAsync();

    public async Task<UserMotiveDto?> GetByIdAsync(Guid id)
    {
        var m = await _context.UserMotives.FindAsync(id);
        return m == null ? null : ToDto(m);
    }

    public async Task<(UserMotiveDto? Dto, string? Error)> CreateAsync(Guid ownerUserId, CreateUserMotiveRequest request)
    {
        var name = request.Name.Trim();
        var exists = await _context.UserMotives.AnyAsync(m => m.OwnerUserId == ownerUserId && m.Name == name);
        if (exists)
            return (null, "A motive with that name already exists.");

        var motive = new UserMotive
        {
            Name = name,
            Color = request.Color?.Trim(),
            OwnerUserId = ownerUserId,
        };
        _context.UserMotives.Add(motive);
        await _context.SaveChangesAsync();
        return (ToDto(motive), null);
    }

    public async Task<UserMotiveDto?> UpdateAsync(Guid id, Guid ownerUserId, UpdateUserMotiveRequest request)
    {
        var motive = await _context.UserMotives.FirstOrDefaultAsync(m => m.Id == id && m.OwnerUserId == ownerUserId);
        if (motive == null) return null;

        motive.Name = request.Name.Trim();
        motive.Color = request.Color?.Trim();
        await _context.SaveChangesAsync();
        return ToDto(motive);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId)
    {
        var motive = await _context.UserMotives.FirstOrDefaultAsync(m => m.Id == id && m.OwnerUserId == ownerUserId);
        if (motive == null) return false;
        _context.UserMotives.Remove(motive);
        await _context.SaveChangesAsync();
        return true;
    }

    internal static UserMotiveDto ToDto(UserMotive m) => new(m.Id, m.Name, m.Color, m.OwnerUserId, m.CreatedAt, m.UpdatedAt);
}
