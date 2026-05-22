using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Domain.Entities.Auth;
using WarcraftArchive.Api.Domain.Entities.Warcraft;

namespace WarcraftArchive.Api.Infrastructure.Persistence;

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
