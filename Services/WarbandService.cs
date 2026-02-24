using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Auth;

namespace WarcraftArchive.Api.Services;

public class WarbandService : IWarbandService
{
    private readonly AppDbContext _context;

    public WarbandService(AppDbContext context) => _context = context;

    public async Task<List<WarbandDto>> GetAllAsync(Guid ownerUserId) =>
        await _context.Warbands
            .Where(w => w.OwnerUserId == ownerUserId)
            .OrderBy(w => w.Name)
            .Select(w => ToDto(w))
            .ToListAsync();

    public async Task<WarbandDto?> GetByIdAsync(Guid id)
    {
        var w = await _context.Warbands.FindAsync(id);
        return w == null ? null : ToDto(w);
    }

    public async Task<(WarbandDto? Dto, string? Error)> CreateAsync(Guid ownerUserId, CreateWarbandRequest request)
    {
        var name = request.Name.Trim();
        var exists = await _context.Warbands.AnyAsync(w => w.OwnerUserId == ownerUserId && w.Name == name);
        if (exists)
            return (null, "A warband with that name already exists.");

        var warband = new Warband
        {
            Name = name,
            Color = request.Color?.Trim(),
            OwnerUserId = ownerUserId,
        };
        _context.Warbands.Add(warband);
        await _context.SaveChangesAsync();
        return (ToDto(warband), null);
    }

    public async Task<WarbandDto?> UpdateAsync(Guid id, Guid ownerUserId, UpdateWarbandRequest request)
    {
        var warband = await _context.Warbands.FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == ownerUserId);
        if (warband == null) return null;

        warband.Name = request.Name.Trim();
        warband.Color = request.Color?.Trim();
        await _context.SaveChangesAsync();
        return ToDto(warband);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId)
    {
        var warband = await _context.Warbands.FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == ownerUserId);
        if (warband == null) return false;
        _context.Warbands.Remove(warband);
        await _context.SaveChangesAsync();
        return true;
    }

    internal static WarbandDto ToDto(Warband w) => new(w.Id, w.Name, w.Color, w.OwnerUserId, w.CreatedAt, w.UpdatedAt);
}
