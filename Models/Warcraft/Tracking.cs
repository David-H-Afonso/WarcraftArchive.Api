namespace WarcraftArchive.Api.Models.Warcraft;

public class Tracking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Guid ContentId { get; set; }
    /// <summary>Single DifficultyFlags value (one of LFR=1, Normal=2, Heroic=4, Mythic=8).</summary>
    public DifficultyFlags Difficulty { get; set; }
    public Frequency Frequency { get; set; }
    public TrackingStatus Status { get; set; } = TrackingStatus.NotStarted;
    public string? Comment { get; set; }

    /// <summary>Optional. Set when Status is updated to Finished/LastDay/LastWeek for analytics.</summary>
    public DateTime? LastCompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Character Character { get; set; } = null!;
    public Content Content { get; set; } = null!;
}
