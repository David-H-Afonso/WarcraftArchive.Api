namespace WarcraftArchive.Api.Models.Warcraft;

public class Content
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Expansion { get; set; } = string.Empty;
    public string? Comment { get; set; }

    /// <summary>
    /// Bitmask of <see cref="DifficultyFlags"/>. Stores which difficulties are valid for this content.
    /// Example: LFR|Normal|Heroic|Mythic = 15.
    /// </summary>
    public int AllowedDifficulties { get; set; }

    /// <summary>
    /// Bitmask of <see cref="MotiveFlags"/>. CSV source: "Mounts,Transmog" → split and OR the flags.
    /// </summary>
    public int Motives { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ICollection<Tracking> Trackings { get; set; } = [];
}
