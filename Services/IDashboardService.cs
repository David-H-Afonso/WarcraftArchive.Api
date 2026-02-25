using WarcraftArchive.Api.DTOs;

namespace WarcraftArchive.Api.Services;

public interface IDashboardService
{
    Task<WeeklyDashboardDto> GetWeeklyAsync(Guid ownerUserId);
}
