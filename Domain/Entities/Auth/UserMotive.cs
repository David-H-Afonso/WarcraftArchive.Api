using WarcraftArchive.Api.Domain.Entities.Warcraft;

namespace WarcraftArchive.Api.Domain.Entities.Auth;

public class UserMotive
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User OwnerUser { get; set; } = null!;
    public ICollection<Content> Contents { get; set; } = [];
}
