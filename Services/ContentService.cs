using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Services;

public class ContentService : IContentService
{
    private readonly AppDbContext _context;
    public ContentService(AppDbContext context) => _context = context;

    public async Task<List<ContentDto>> GetAllAsync(string? search)
    {
        var query = _context.Contents.Include(c => c.Motives).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || c.Expansion.ToLower().Contains(s));
        }
        var list = await query.OrderBy(c => c.Expansion).ThenBy(c => c.Name).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ContentDto?> GetByIdAsync(Guid id)
    {
        var c = await _context.Contents.Include(co => co.Motives).FirstOrDefaultAsync(co => co.Id == id);
        return c == null ? null : ToDto(c);
    }

    public async Task<ContentDto> CreateAsync(CreateContentRequest request)
    {
        var motives = await _context.UserMotives.Where(m => request.MotiveIds.Contains(m.Id)).ToListAsync();
        var content = new Content
        {
            Name = request.Name.Trim(),
            Expansion = request.Expansion.Trim(),
            Comment = request.Comment?.Trim(),
            AllowedDifficulties = request.AllowedDifficulties,
            Motives = motives,
        };
        _context.Contents.Add(content);
        await _context.SaveChangesAsync();
        return ToDto(content);
    }

    public async Task<ContentDto?> UpdateAsync(Guid id, UpdateContentRequest request)
    {
        var content = await _context.Contents.Include(co => co.Motives).FirstOrDefaultAsync(co => co.Id == id);
        if (content == null) return null;
        var motives = await _context.UserMotives.Where(m => request.MotiveIds.Contains(m.Id)).ToListAsync();
        content.Name = request.Name.Trim();
        content.Expansion = request.Expansion.Trim();
        content.Comment = request.Comment?.Trim();
        content.AllowedDifficulties = request.AllowedDifficulties;
        content.Motives.Clear();
        foreach (var m in motives) content.Motives.Add(m);
        await _context.SaveChangesAsync();
        return ToDto(content);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var content = await _context.Contents.FindAsync(id);
        if (content == null) return false;
        _context.Contents.Remove(content);
        await _context.SaveChangesAsync();
        return true;
    }

    internal static ContentDto ToDto(Content c) => new(
        c.Id, c.Name, c.Expansion, c.Comment,
        c.AllowedDifficulties,
        c.Motives.Select(m => UserMotiveService.ToDto(m)).ToList(),
        c.CreatedAt, c.UpdatedAt);
}
