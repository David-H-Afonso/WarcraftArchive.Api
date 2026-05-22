using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WarcraftArchive.Api.Infrastructure.Persistence;
using WarcraftArchive.Api.Application.Interfaces;
using WarcraftArchive.Api.Application.Services;

namespace WarcraftArchive.Api.Configuration;

public static class ServiceCollectionExtensions
{
    // ── .env file loading (local development) ────────────────────────────────

    public static WebApplicationBuilder LoadEnvironmentFile(this WebApplicationBuilder builder)
    {
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
        return builder;
    }

    // ── Environment variable overrides (12-factor) ───────────────────────────

    public static WebApplicationBuilder ApplyEnvironmentOverrides(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;

        ApplyEnvOverride(config, "DatabaseSettings:DatabasePath", "DATABASE_PATH");
        ApplyEnvOverride(config, "JwtSettings:SecretKey", "JWT_SECRET_KEY");
        ApplyEnvOverride(config, "JwtSettings:Issuer", "JWT_ISSUER");
        ApplyEnvOverride(config, "JwtSettings:Audience", "JWT_AUDIENCE");
        ApplyEnvOverrideInt(config, "JwtSettings:AccessTokenMinutes", "JWT_ACCESS_TOKEN_MINUTES");
        ApplyEnvOverrideInt(config, "JwtSettings:RefreshTokenDays", "JWT_REFRESH_TOKEN_DAYS");
        ApplyEnvOverride(config, "SeedSettings:AdminEmail", "SEED_ADMIN_EMAIL");
        ApplyEnvOverride(config, "SeedSettings:AdminUsername", "SEED_ADMIN_USERNAME");
        ApplyEnvOverride(config, "SeedSettings:AdminPassword", "SEED_ADMIN_PASSWORD");
        ApplyEnvOverrideBool(config, "SeedSettings:AdminEnabled", "SEED_ADMIN_ENABLED");
        ApplyEnvOverrideBool(config, "SeedSettings:DemoImportEnabled", "DEMO_IMPORT_ENABLED");
        ApplyEnvOverride(config, "SeedSettings:CsvDataPath", "CSV_DATA_PATH");

        var corsOriginEnv =
            Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ??
            Environment.GetEnvironmentVariable("FRONTEND_URL");
        if (!string.IsNullOrWhiteSpace(corsOriginEnv))
        {
            var origins = corsOriginEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < origins.Length; i++)
                config[$"CorsSettings:AllowedOrigins:{i}"] = origins[i];
        }

        return builder;
    }

    // ── Configuration binding ────────────────────────────────────────────────

    public static IServiceCollection BindConfigurationSections(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));
        services.Configure<SeedSettings>(configuration.GetSection(SeedSettings.SectionName));
        return services;
    }

    // ── Database ─────────────────────────────────────────────────────────────

    public static IServiceCollection AddWarcraftArchiveDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var dbSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>() ?? new();
        var dbPath = dbSettings.DatabasePath;
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.GetFullPath(dbPath);
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
            Directory.CreateDirectory(dbDir);

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
            if (dbSettings.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
        });

        return services;
    }

    // ── Authentication & Authorization ───────────────────────────────────────

    public static IServiceCollection AddWarcraftArchiveAuth(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new();
        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
            throw new InvalidOperationException(
                "JWT SecretKey is not configured. Set JWT_SECRET_KEY environment variable.");

        if (environment.IsProduction())
        {
            var insecureDefaults = new[] { "CHANGE_THIS_TO_A_SECURE_KEY_IN_PRODUCTION", "DevOnlySecretKeyThatIsAtLeast32CharactersLong!" };
            if (insecureDefaults.Contains(jwtSettings.SecretKey, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "JWT SecretKey is using a known default value in Production. Set a secure JWT_SECRET_KEY environment variable.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

        services.AddAuthorization();

        return services;
    }

    // ── CORS ─────────────────────────────────────────────────────────────────

    public static IServiceCollection AddWarcraftArchiveCors(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new();
        if (corsSettings.AllowedOrigins.Count == 0 && !environment.IsDevelopment())
            throw new InvalidOperationException(
                "CORS: no AllowedOrigins configured for production. Set CORS_ALLOWED_ORIGINS env var.");

        services.AddCors(options =>
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

        return services;
    }

    // ── Swagger ──────────────────────────────────────────────────────────────

    public static IServiceCollection AddWarcraftArchiveSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
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

        return services;
    }

    // ── Services ─────────────────────────────────────────────────────────────

    public static IServiceCollection AddWarcraftArchiveServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IWarbandService, WarbandService>();
        services.AddScoped<IUserMotiveService, UserMotiveService>();
        services.AddScoped<IDataService, DataService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IResetService, ResetService>();
        services.AddHttpContextAccessor();
        return services;
    }

    // ── Private env override helpers ─────────────────────────────────────────

    private static void ApplyEnvOverride(IConfigurationRoot config, string key, string envVar)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(value)) config[key] = value;
    }

    private static void ApplyEnvOverrideInt(IConfigurationRoot config, string key, string envVar)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (int.TryParse(value, out _)) config[key] = value!;
    }

    private static void ApplyEnvOverrideBool(IConfigurationRoot config, string key, string envVar)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(value)) config[key] = value;
    }
}
