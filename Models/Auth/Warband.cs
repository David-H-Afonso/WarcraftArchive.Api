namespace WarcraftArchive.Api.Models.Auth;

public class Warband
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SortOrder { get; set; } = 0;
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User OwnerUser { get; set; } = null!;
    public ICollection<Warcraft.Character> Characters { get; set; } = [];
}
