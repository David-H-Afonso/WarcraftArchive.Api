using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Models.Auth;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        ChangeTracker.LazyLoadingEnabled = false;
    }

    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Warband> Warbands { get; set; }
    public DbSet<UserMotive> UserMotives { get; set; }
    public DbSet<Character> Characters { get; set; }
    public DbSet<Content> Contents { get; set; }
    public DbSet<Tracking> Trackings { get; set; }

    public override int SaveChanges() { UpdateTimestamps(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { UpdateTimestamps(); return base.SaveChangesAsync(cancellationToken); }

    private void UpdateTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                switch (entry.Entity)
                {
                    case User u: u.CreatedAt = now; u.UpdatedAt = now; break;
                    case Character c: c.CreatedAt = now; c.UpdatedAt = now; break;
                    case Content co: co.CreatedAt = now; co.UpdatedAt = now; break;
                    case Tracking t: t.CreatedAt = now; t.UpdatedAt = now; break;
                    case RefreshToken rt: rt.CreatedAt = now; break;
                    case Warband wb: wb.CreatedAt = now; wb.UpdatedAt = now; break;
                    case UserMotive um: um.CreatedAt = now; um.UpdatedAt = now; break;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                switch (entry.Entity)
                {
                    case User u: u.UpdatedAt = now; entry.Property(nameof(User.CreatedAt)).IsModified = false; break;
                    case Character c: c.UpdatedAt = now; entry.Property(nameof(Character.CreatedAt)).IsModified = false; break;
                    case Content co: co.UpdatedAt = now; entry.Property(nameof(Content.CreatedAt)).IsModified = false; break;
                    case Tracking t: t.UpdatedAt = now; entry.Property(nameof(Tracking.CreatedAt)).IsModified = false; break;
                    case Warband wb: wb.UpdatedAt = now; entry.Property(nameof(Warband.CreatedAt)).IsModified = false; break;
                    case UserMotive um: um.UpdatedAt = now; entry.Property(nameof(UserMotive.CreatedAt)).IsModified = false; break;
                }
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).IsRequired().HasMaxLength(320);
            e.Property(u => u.UserName).IsRequired().HasMaxLength(100);
            e.Property(u => u.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);
            e.HasIndex(rt => rt.TokenHash).IsUnique();
            e.HasIndex(rt => rt.UserId);
            e.Property(rt => rt.TokenHash).IsRequired();
            e.HasOne(rt => rt.User).WithMany(u => u.RefreshTokens).HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Warband>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => new { w.OwnerUserId, w.Name }).IsUnique();
            e.Property(w => w.Name).IsRequired().HasMaxLength(200);
            e.Property(w => w.Color).HasMaxLength(50);
            e.HasOne(w => w.OwnerUser).WithMany(u => u.Warbands).HasForeignKey(w => w.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserMotive>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.OwnerUserId, m.Name }).IsUnique();
            e.Property(m => m.Name).IsRequired().HasMaxLength(200);
            e.Property(m => m.Color).HasMaxLength(50);
            e.HasOne(m => m.OwnerUser).WithMany(u => u.UserMotives).HasForeignKey(m => m.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Character>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Name);
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.Property(c => c.Class).IsRequired().HasMaxLength(100);
            e.Property(c => c.Race).HasMaxLength(100);
            e.Property(c => c.Covenant).HasMaxLength(100);
            e.HasOne(c => c.OwnerUser).WithMany().HasForeignKey(c => c.OwnerUserId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasOne(c => c.Warband).WithMany(w => w.Characters).HasForeignKey(c => c.WarbandId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        modelBuilder.Entity<Content>(e =>
        {
            e.HasKey(co => co.Id);
            e.HasIndex(co => co.Name);
            e.HasIndex(co => co.Expansion);
            e.HasIndex(co => co.OwnerUserId);
            e.Property(co => co.Name).IsRequired().HasMaxLength(300);
            e.Property(co => co.Expansion).IsRequired().HasMaxLength(100);
            e.Property(co => co.Comment).HasMaxLength(1000);
            e.HasMany(co => co.Motives).WithMany(m => m.Contents).UsingEntity("ContentUserMotives");
            e.HasOne(co => co.OwnerUser).WithMany().HasForeignKey(co => co.OwnerUserId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        modelBuilder.Entity<Tracking>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.CharacterId, t.ContentId, t.Difficulty }).IsUnique();
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.Frequency);
            e.Property(t => t.Comment).HasMaxLength(1000);
            e.HasOne(t => t.Character).WithMany(c => c.Trackings).HasForeignKey(t => t.CharacterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Content).WithMany(co => co.Trackings).HasForeignKey(t => t.ContentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
