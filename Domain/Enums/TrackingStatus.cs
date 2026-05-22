namespace WarcraftArchive.Api.Domain.Enums;

/// <summary>
/// Tracking progress status.
/// LastDay (3) = completed in the previous daily period.
/// LastWeek (4) = completed in the previous weekly period.
/// </summary>
public enum TrackingStatus
{
    NotStarted = 0,
    Pending = 1,
    InProgress = 2,
    LastDay = 3,
    LastWeek = 4,
    Finished = 5,
}
