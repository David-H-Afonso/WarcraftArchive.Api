using WarcraftArchive.Api.Domain.Entities.Auth;

namespace WarcraftArchive.Api.Domain.Entities.Warcraft;

public class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int? Level { get; set; }
    public string Class { get; set; } = string.Empty;
    public string? Race { get; set; }
    public string? Covenant { get; set; }
    public Guid? WarbandId { get; set; }

    /// <summary>
    /// Owner — nullable to allow future multi-user scenarios.
    /// Seed/import creates characters under the admin user.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User? OwnerUser { get; set; }
    public Warband? Warband { get; set; }
    public ICollection<Tracking> Trackings { get; set; } = [];
}
