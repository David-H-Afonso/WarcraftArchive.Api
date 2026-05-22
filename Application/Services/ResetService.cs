using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Application.Interfaces;
using WarcraftArchive.Api.Infrastructure.Persistence;
using WarcraftArchive.Api.Domain.Enums;

namespace WarcraftArchive.Api.Application.Services;

public class ResetService : IResetService
{
    private readonly AppDbContext _db;

    public ResetService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> ApplyDailyResetAsync()
    {
        var trackings = await _db.Trackings
            .Where(t => t.Frequency == Frequency.Daily &&
                        (t.Status == TrackingStatus.Finished ||
                         t.Status == TrackingStatus.LastDay ||
                         t.Status == TrackingStatus.InProgress ||
                         t.Status == TrackingStatus.Pending))
            .ToListAsync();

        foreach (var t in trackings)
            t.Status = t.Status == TrackingStatus.Finished
                ? TrackingStatus.LastDay
                : TrackingStatus.NotStarted; // LastDay, InProgress, Pending → NotStarted

        if (trackings.Count > 0)
            await _db.SaveChangesAsync();

        return trackings.Count;
    }

    public async Task<(int Weekly, int Daily)> ApplyWeeklyResetAsync()
    {
        var weeklyTrackings = await _db.Trackings
            .Where(t => t.Frequency == Frequency.Weekly &&
                        (t.Status == TrackingStatus.Finished ||
                         t.Status == TrackingStatus.LastWeek ||
                         t.Status == TrackingStatus.InProgress ||
                         t.Status == TrackingStatus.Pending))
            .ToListAsync();

        foreach (var t in weeklyTrackings)
            t.Status = t.Status == TrackingStatus.Finished
                ? TrackingStatus.LastWeek
                : TrackingStatus.NotStarted; // LastWeek, InProgress, Pending → NotStarted

        if (weeklyTrackings.Count > 0)
            await _db.SaveChangesAsync();

        // A weekly reset also implies a daily reset
        var daily = await ApplyDailyResetAsync();

        return (weeklyTrackings.Count, daily);
    }
}
