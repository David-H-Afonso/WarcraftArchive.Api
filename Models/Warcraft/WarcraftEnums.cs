namespace WarcraftArchive.Api.Models.Warcraft;

/// <summary>
/// Unified bitmask for difficulties used both in Content.AllowedDifficulties
/// and as individual Tracking.Difficulty values (single flag per tracking row).
/// LFR=1, Normal=2, Heroic=4, Mythic=8.
/// </summary>
[Flags]
public enum DifficultyFlags
{
    None = 0,
    LFR = 1,
    Normal = 2,
    Heroic = 4,
    Mythic = 8,
}

public enum Frequency
{
    Hourly = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
}

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

/// <summary>Used as a bitmask on Content.Motives (Notion CSV: "Mounts,Transmog").</summary>
[Flags]
public enum MotiveFlags
{
    None = 0,
    Mounts = 1,
    Transmog = 2,
    Reputation = 4,
    Anima = 8,
    Achievement = 16,
}
