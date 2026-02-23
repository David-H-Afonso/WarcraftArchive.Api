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
        var query = _context.Contents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(s) ||
                c.Expansion.ToLower().Contains(s));
        }

        return await query
            .OrderBy(c => c.Expansion)
            .ThenBy(c => c.Name)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task<ContentDto?> GetByIdAsync(Guid id)
    {
        var c = await _context.Contents.FindAsync(id);
        return c == null ? null : ToDto(c);
    }

    public async Task<ContentDto> CreateAsync(CreateContentRequest request)
    {
        var content = new Content
        {
            Name = request.Name.Trim(),
            Expansion = request.Expansion.Trim(),
            Comment = request.Comment?.Trim(),
            AllowedDifficulties = (int)request.AllowedDifficulties,
            Motives = (int)request.Motives,
        };
        _context.Contents.Add(content);
        await _context.SaveChangesAsync();
        return ToDto(content);
    }

    public async Task<ContentDto?> UpdateAsync(Guid id, UpdateContentRequest request)
    {
        var content = await _context.Contents.FindAsync(id);
        if (content == null)
            return null;

        content.Name = request.Name.Trim();
        content.Expansion = request.Expansion.Trim();
        content.Comment = request.Comment?.Trim();
        content.AllowedDifficulties = (int)request.AllowedDifficulties;
        content.Motives = (int)request.Motives;

        await _context.SaveChangesAsync();
        return ToDto(content);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var content = await _context.Contents.FindAsync(id);
        if (content == null)
            return false;
        _context.Contents.Remove(content);
        await _context.SaveChangesAsync();
        return true;
    }

    internal static ContentDto ToDto(Content c) => new(
        c.Id, c.Name, c.Expansion, c.Comment,
        (DifficultyFlags)c.AllowedDifficulties,
        (MotiveFlags)c.Motives,
        c.CreatedAt, c.UpdatedAt);
}
