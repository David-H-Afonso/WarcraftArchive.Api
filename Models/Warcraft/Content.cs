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

    /// <summary>Owner — the user who created this content entry.</summary>
    public Guid? OwnerUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Auth.User? OwnerUser { get; set; }
    public ICollection<Tracking> Trackings { get; set; } = [];
    public ICollection<Auth.UserMotive> Motives { get; set; } = [];
}
