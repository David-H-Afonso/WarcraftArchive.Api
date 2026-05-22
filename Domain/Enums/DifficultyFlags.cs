namespace WarcraftArchive.Api.Domain.Enums;

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
