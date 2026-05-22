using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Configuration;
using WarcraftArchive.Api.Domain.Entities.Auth;

namespace WarcraftArchive.Api.Infrastructure.Persistence;

public class DatabaseStartupHelper
{
    public AppDbContext Context { get; }
    private readonly ILogger _logger;

    public DatabaseStartupHelper(AppDbContext context, ILogger<DatabaseStartupHelper> logger)
    {
        Context = context;
        _logger = logger;
    }

    public async Task<User?> GetFirstAdminAsync()
        => await Context.Users.FirstOrDefaultAsync(u => u.IsAdmin);

    // Handles two scenarios:
    //   1. Fresh install        → runs the single consolidated migration normally.
    //   2. Existing legacy DB   → patches any missing schema changes caused by the
    //      old multi-migration history, then rewrites __EFMigrationsHistory so EF
    //      recognises the DB as fully up-to-date with the consolidated migration.
    public async Task ApplyMigrationsAsync()
    {
        // Legacy migration IDs that existed before consolidation
        string[] legacyIds =
        [
            "20260223170050_InitialCreate",
            "20260224175114_AddWarbandMotiveRace",
            "20260225000000_UnifyDifficultyBitmaskAndSplitLastStatus",
            "20260225120000_AddOwnerUserIdToContent",
        ];

        List<string> applied;
        try
        {
            applied = (await Context.Database.GetAppliedMigrationsAsync()).ToList();
        }
        catch
        {
            // __EFMigrationsHistory doesn't exist yet → brand-new database
            applied = [];
        }

        bool hasLegacy = applied.Any(m => legacyIds.Contains(m));

        if (!hasLegacy)
        {
            // Normal path: fresh DB or already on the consolidated migration
            await Context.Database.MigrateAsync();
            return;
        }

        _logger.LogInformation("Legacy migration history detected – patching schema and consolidating history.");

        var conn = Context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        // ── 1. Ensure OwnerUserId exists on Contents (may be missing if DB was
        //        stuck before the 4th legacy migration ran)
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Contents') WHERE name='OwnerUserId'";
            var exists = (long)(await cmd.ExecuteScalarAsync())!;
            if (exists == 0)
            {
                _logger.LogInformation("Adding missing OwnerUserId column to Contents table.");
                cmd.CommandText = "ALTER TABLE Contents ADD COLUMN OwnerUserId TEXT NULL";
                await cmd.ExecuteNonQueryAsync();
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Contents_OwnerUserId ON Contents(OwnerUserId)";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // ── 2. Rewrite migration history to the single consolidated migration
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM __EFMigrationsHistory";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) " +
                              "VALUES ('20260226000000_InitialCreate', '9.0.0')";
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Schema consolidation complete – database is now on migration 20260226000000_InitialCreate.");
    }

    public async Task<User?> SeedAdminAsync(SeedSettings settings)
    {
        if (!settings.AdminEnabled) return null;
        var existing = await Context.Users.FirstOrDefaultAsync(u => u.IsAdmin);
        if (existing != null)
        {
            await SeedDefaultUserDataAsync(existing.Id);
            return existing;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(settings.AdminPassword, workFactor: 12);
        var admin = new User
        {
            Email = settings.AdminEmail,
            UserName = settings.AdminUsername,
            PasswordHash = passwordHash,
            IsAdmin = true,
            IsActive = true,
        };
        Context.Users.Add(admin);
        await Context.SaveChangesAsync();
        _logger.LogInformation("Seed: admin user created ({Email})", settings.AdminEmail);
        await SeedDefaultUserDataAsync(admin.Id);
        return admin;
    }

    public async Task SeedDefaultUserDataAsync(Guid userId)
    {
        // Default warband
        if (!await Context.Warbands.AnyAsync(w => w.OwnerUserId == userId && w.Name == "Favourites"))
        {
            Context.Warbands.Add(new Warband { Name = "Favourites", Color = "#7c8cff", OwnerUserId = userId });
            _logger.LogInformation("Seed: default warband created for user {UserId}", userId);
        }

        // Default motives
        var defaultMotives = new[]
        {
            ("Mounts",      "#e8a44a"),
            ("Transmog",    "#a855f7"),
            ("Achievement", "#3b82f6"),
            ("Anima",       "#6366f1"),
            ("Reputation",  "#10b981"),
            ("Toys",        "#ec4899"),
        };
        foreach (var (name, color) in defaultMotives)
        {
            if (!await Context.UserMotives.AnyAsync(m => m.OwnerUserId == userId && m.Name == name))
            {
                Context.UserMotives.Add(new UserMotive { Name = name, Color = color, OwnerUserId = userId });
            }
        }

        await Context.SaveChangesAsync();
    }
}
