using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;

    public DashboardService(AppDbContext context) => _context = context;

    /// <summary>
    /// Aggregates all Weekly trackings grouped by status, plus the full list.
    /// Used as the home screen overview for farming progress.
    /// </summary>
    public async Task<WeeklyDashboardDto> GetWeeklyAsync()
    {
        var items = await _context.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content)
            .Where(t => t.Frequency == Frequency.Weekly)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Content.Expansion)
            .ThenBy(t => t.Content.Name)
            .ToListAsync();

        return new WeeklyDashboardDto(
            Total: items.Count,
            NotStarted: items.Count(t => t.Status == TrackingStatus.NotStarted),
            Pending: items.Count(t => t.Status == TrackingStatus.Pending),
            InProgress: items.Count(t => t.Status == TrackingStatus.InProgress),
            LastWeek: items.Count(t => t.Status == TrackingStatus.LastWeek),
            Finished: items.Count(t => t.Status == TrackingStatus.Finished),
            Items: items.Select(TrackingService.ToDto).ToList());
    }
}
