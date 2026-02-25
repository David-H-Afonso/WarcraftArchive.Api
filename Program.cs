using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WarcraftArchive.Api.Configuration;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.Endpoints;
using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Middleware;
using WarcraftArchive.Api.Models.Auth;
using WarcraftArchive.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── .env file loading (local development) ────────────────────────────────────
var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFilePath))
{
    foreach (var line in File.ReadAllLines(envFilePath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}

// ── Environment variable overrides (12-factor) ────────────────────────────────
ApplyEnvOverride(builder.Configuration, "DatabaseSettings:DatabasePath", "DATABASE_PATH");
ApplyEnvOverride(builder.Configuration, "JwtSettings:SecretKey", "JWT_SECRET_KEY");
ApplyEnvOverride(builder.Configuration, "JwtSettings:Issuer", "JWT_ISSUER");
ApplyEnvOverride(builder.Configuration, "JwtSettings:Audience", "JWT_AUDIENCE");
ApplyEnvOverrideInt(builder.Configuration, "JwtSettings:AccessTokenMinutes", "JWT_ACCESS_TOKEN_MINUTES");
ApplyEnvOverrideInt(builder.Configuration, "JwtSettings:RefreshTokenDays", "JWT_REFRESH_TOKEN_DAYS");
ApplyEnvOverride(builder.Configuration, "SeedSettings:AdminEmail", "SEED_ADMIN_EMAIL");
ApplyEnvOverride(builder.Configuration, "SeedSettings:AdminUsername", "SEED_ADMIN_USERNAME");
ApplyEnvOverride(builder.Configuration, "SeedSettings:AdminPassword", "SEED_ADMIN_PASSWORD");
ApplyEnvOverrideBool(builder.Configuration, "SeedSettings:AdminEnabled", "SEED_ADMIN_ENABLED");
ApplyEnvOverrideBool(builder.Configuration, "SeedSettings:DemoImportEnabled", "DEMO_IMPORT_ENABLED");
ApplyEnvOverride(builder.Configuration, "SeedSettings:CsvDataPath", "CSV_DATA_PATH");

var corsOriginEnv =
    Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ??
    Environment.GetEnvironmentVariable("FRONTEND_URL");
if (!string.IsNullOrWhiteSpace(corsOriginEnv))
{
    var origins = corsOriginEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    for (var i = 0; i < origins.Length; i++)
        builder.Configuration[$"CorsSettings:AllowedOrigins:{i}"] = origins[i];
}

// ── Configuration binding ─────────────────────────────────────────────────────
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(CorsSettings.SectionName));
builder.Services.Configure<SeedSettings>(builder.Configuration.GetSection(SeedSettings.SectionName));

// ── Database ──────────────────────────────────────────────────────────────────
var dbSettings = builder.Configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>() ?? new();
var dbPath = dbSettings.DatabasePath;
if (!Path.IsPathRooted(dbPath))
    dbPath = Path.GetFullPath(dbPath);
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir))
    Directory.CreateDirectory(dbDir);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
    if (dbSettings.EnableSensitiveDataLogging)
        options.EnableSensitiveDataLogging();
});

// ── Authentication & Authorization ────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new();
if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
    throw new InvalidOperationException(
        "JWT SecretKey is not configured. Set JWT_SECRET_KEY environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                if (ctx.Exception.Message.Contains("expired"))
                    logger.LogWarning("JWT token expired");
                else
                    logger.LogError(ctx.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────────────────────────────
var corsSettings = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new();
if (corsSettings.AllowedOrigins.Count == 0 && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "CORS: no AllowedOrigins configured for production. Set CORS_ALLOWED_ORIGINS env var.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        if (corsSettings.AllowedOrigins.Count > 0)
            policy.WithOrigins(corsSettings.AllowedOrigins.ToArray()).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "WarcraftArchive API",
        Version = "v1",
        Description = "Backend para trackear farmeos de World of Warcraft: personajes, contenido e instancias.",
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Bearer. Formato: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IWarbandService, WarbandService>();
builder.Services.AddScoped<IUserMotiveService, UserMotiveService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Migrate & Seed ────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var appLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    db.Database.Migrate();

    var seedCfg = scope.ServiceProvider.GetRequiredService<IConfiguration>()
        .GetSection(SeedSettings.SectionName).Get<SeedSettings>() ?? new();

    var adminUser = await SeedAdminAsync(db, seedCfg, appLogger);

    if (seedCfg.DemoImportEnabled)
    {
        var importUser = adminUser ?? await db.Users.FirstOrDefaultAsync(u => u.IsAdmin);
        if (importUser != null)
            await CsvImportHelper.ImportAsync(db, seedCfg.CsvDataPath, importUser, appLogger);
        else
            appLogger.LogWarning("CsvImport: no admin user found — skipping CSV import.");
    }
}

// ── Middleware pipeline ────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "WarcraftArchive API v1");
    c.RoutePrefix = "swagger";
});
app.UseMiddleware<ErrorHandlingMiddleware>();
if (app.Environment.IsDevelopment())
    app.UseCors("AllowAll");
else
    app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();

// ── Health ────────────────────────────────────────────────────────────────────
app.MapGet("/health", async (AppDbContext db, ILogger<Program> logger) =>
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    string dbStatus;
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        dbStatus = pending.Count == 0 ? "ok" : $"ok ({pending.Count} pending migration(s))";
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Health check: DB connectivity failed");
        dbStatus = "error";
    }
    var overall = dbStatus.StartsWith("error") ? "degraded" : "healthy";
    var payload = new { status = overall, version, db = dbStatus, timestamp = DateTime.UtcNow };
    return overall == "healthy" ? Results.Ok(payload) : Results.Json(payload, statusCode: 503);
}).AllowAnonymous().WithTags("System");

// ── Routes ────────────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapCharacterEndpoints();
app.MapContentEndpoints();
app.MapTrackingEndpoints();
app.MapDashboardEndpoints();
app.MapWarbandEndpoints();
app.MapUserMotiveEndpoints();
app.MapDataEndpoints();
app.MapResetEndpoints();

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────

static async Task<User?> SeedAdminAsync(AppDbContext db, SeedSettings settings, ILogger logger)
{
    if (!settings.AdminEnabled) return null;
    var existing = await db.Users.FirstOrDefaultAsync(u => u.IsAdmin);
    if (existing != null)
    {
        await SeedDefaultUserDataAsync(db, existing.Id, logger);
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
    db.Users.Add(admin);
    await db.SaveChangesAsync();
    logger.LogInformation("Seed: admin user created ({Email})", settings.AdminEmail);
    await SeedDefaultUserDataAsync(db, admin.Id, logger);
    return admin;
}

static async Task SeedDefaultUserDataAsync(AppDbContext db, Guid userId, ILogger logger)
{
    // Default warband
    if (!await db.Warbands.AnyAsync(w => w.OwnerUserId == userId && w.Name == "Favourites"))
    {
        db.Warbands.Add(new Warband { Name = "Favourites", Color = "#7c8cff", OwnerUserId = userId });
        logger.LogInformation("Seed: default warband created for user {UserId}", userId);
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
        if (!await db.UserMotives.AnyAsync(m => m.OwnerUserId == userId && m.Name == name))
        {
            db.UserMotives.Add(new UserMotive { Name = name, Color = color, OwnerUserId = userId });
        }
    }

    await db.SaveChangesAsync();
}

static void ApplyEnvOverride(IConfigurationRoot config, string key, string envVar)
{
    var value = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrWhiteSpace(value)) config[key] = value;
}
static void ApplyEnvOverrideInt(IConfigurationRoot config, string key, string envVar)
{
    var value = Environment.GetEnvironmentVariable(envVar);
    if (int.TryParse(value, out _)) config[key] = value!;
}
static void ApplyEnvOverrideBool(IConfigurationRoot config, string key, string envVar)
{
    var value = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrWhiteSpace(value)) config[key] = value;
}
