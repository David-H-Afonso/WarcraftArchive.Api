using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Services;

public class ContentService : IContentService
{
    private readonly AppDbContext _context;
    public ContentService(AppDbContext context) => _context = context;

    public async Task<List<ContentDto>> GetAllAsync(Guid ownerUserId, string? expansion = null)
    {
        var query = _context.Contents
            .Include(c => c.Motives)
            .Where(c => c.OwnerUserId == ownerUserId)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(expansion))
        {
            var exp = expansion.Trim().ToLower();
            query = query.Where(c => c.Expansion.ToLower() == exp);
        }
        var list = await query.OrderBy(c => c.Expansion).ThenBy(c => c.Name).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ContentDto?> GetByIdAsync(Guid id)
    {
        var c = await _context.Contents.Include(co => co.Motives).FirstOrDefaultAsync(co => co.Id == id);
        return c == null ? null : ToDto(c);
    }

    public async Task<ContentDto> CreateAsync(Guid ownerUserId, CreateContentRequest request)
    {
        var motives = await _context.UserMotives.Where(m => request.MotiveIds.Contains(m.Id)).ToListAsync();
        var content = new Content
        {
            Name = request.Name.Trim(),
            Expansion = request.Expansion.Trim(),
            Comment = request.Comment?.Trim(),
            AllowedDifficulties = request.AllowedDifficulties,
            OwnerUserId = ownerUserId,
            Motives = motives,
        };
        _context.Contents.Add(content);
        await _context.SaveChangesAsync();
        await _context.Entry(content).Collection(c => c.Motives).LoadAsync();
        return ToDto(content);
    }

    public async Task<ContentDto?> UpdateAsync(Guid id, Guid ownerUserId, UpdateContentRequest request)
    {
        var content = await _context.Contents.Include(co => co.Motives)
            .FirstOrDefaultAsync(co => co.Id == id && co.OwnerUserId == ownerUserId);
        if (content == null) return null;
        content.Name = request.Name.Trim();
        content.Expansion = request.Expansion.Trim();
        content.Comment = request.Comment?.Trim();
        content.AllowedDifficulties = request.AllowedDifficulties;
        var motives = await _context.UserMotives.Where(m => request.MotiveIds.Contains(m.Id)).ToListAsync();
        content.Motives = motives;
        await _context.SaveChangesAsync();
        return ToDto(content);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId)
    {
        var content = await _context.Contents.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId);
        if (content == null) return false;
        _context.Contents.Remove(content);
        await _context.SaveChangesAsync();
        return true;
    }

    private static ContentDto ToDto(Content c) => new(
        c.Id, c.Name, c.Expansion, c.Comment, c.AllowedDifficulties,
        c.Motives.Select(m => new UserMotiveDto(m.Id, m.Name, m.Color, m.OwnerUserId, m.CreatedAt, m.UpdatedAt)).ToList(),
        c.CreatedAt, c.UpdatedAt);
}
