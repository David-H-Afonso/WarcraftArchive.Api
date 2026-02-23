namespace WarcraftArchive.Api.Models.Warcraft;

/// <summary>Used as a bitmask on Content.AllowedDifficulties and for individual Tracking rows.</summary>
[Flags]
public enum DifficultyFlags
{
    None = 0,
    LFR = 1,
    Normal = 2,
    Heroic = 4,
    Mythic = 8,
}

/// <summary>Single difficulty value assigned to a Tracking row.</summary>
public enum Difficulty
{
    LFR = 0,
    Normal = 1,
    Heroic = 2,
    Mythic = 3,
}

public enum Frequency
{
    Hourly = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
}

public enum TrackingStatus
{
    NotStarted = 0,
    Pending = 1,
    InProgress = 2,
    LastWeek = 3,
    Finished = 4,
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
