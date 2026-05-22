# Backend Implementation Guide — WarcraftArchive API

This document explains how the WarcraftArchive backend works and how to implement future changes. It is written for a developer who is stronger on frontend and wants to understand the backend well enough to add features, fix bugs, and maintain the project confidently.

---

## 1. Backend Overview

WarcraftArchive is a self-hosted application that tracks World of Warcraft farming progress. The backend is a REST API that:

- **Manages characters** — WoW characters grouped into warbands, with class, race, level, and covenant tracking
- **Manages content** — Raids, dungeons, and other farmable instances organized by expansion with difficulty bitmasks
- **Tracks farming progress** — Per-character, per-content, per-difficulty tracking entries with status cycles and frequencies
- **Provides a farming dashboard** — Weekly summary with status breakdowns for the home screen
- **Supports user motives/goals** — Tags like "Mounts", "Transmog", "Achievement" linked to content
- **Handles daily/weekly resets** — Admin-triggered status transitions that mirror WoW's reset schedule
- **Manages data import/export** — CSV export/import for characters, content, and progress
- **Handles orphan management** — Admin tools to claim or delete data left without an owner
- **Supports multi-user authentication** — JWT with refresh token rotation and admin user management
- **Imports legacy Notion data** — CSV import from Notion-exported spreadsheets on startup

The main consumer is the React frontend at `WarcraftArchive.Front/`.

---

## 2. Technology Stack

| Technology            | Version                                             | Purpose                         |
| --------------------- | --------------------------------------------------- | ------------------------------- |
| .NET                  | 9.0                                                 | Runtime                         |
| ASP.NET Core          | 9.0                                                 | Web API framework (Minimal API) |
| Entity Framework Core | 9.0                                                 | ORM / database access           |
| SQLite                | via `Microsoft.EntityFrameworkCore.Sqlite`          | Database                        |
| JWT Bearer            | `Microsoft.AspNetCore.Authentication.JwtBearer` 9.0 | Authentication                  |
| BCrypt.Net-Next       | 4.0.3                                               | Password hashing                |
| Swashbuckle           | 7.2.0                                               | Swagger / OpenAPI documentation |
| Docker                | Dockerfile included                                 | Containerized deployment        |

**Not present:** No test framework, no FluentValidation, no AutoMapper, no MediatR, no repository pattern, no controllers (uses Minimal API endpoints).

---

## 3. Project Structure Explained

```
WarcraftArchive.Api/
  Application/
    Interfaces/     ← 10 service interfaces (IAuthService, ICharacterService, IContentService,
                       ITrackingService, IDashboardService, IWarbandService, IUserMotiveService,
                       IAdminService, IDataService, IResetService)
    Services/       ← 10 service implementations
  Common/           ← HttpContextExtensions.cs, CsvImportHelper.cs
  Configuration/    ← ServiceCollectionExtensions.cs, DatabaseSettings, JwtSettings, CorsSettings, SeedSettings
  Contracts/        ← AuthContracts, CharacterContracts, ContentContracts, TrackingContracts,
                       WarbandContracts, UserMotiveContracts
  Domain/
    Entities/
      Auth/         ← User, RefreshToken, UserMotive, Warband
      Warcraft/     ← Character, Content, Tracking
    Enums/          ← DifficultyFlags, Frequency, TrackingStatus
  Endpoints/        ← 10 Minimal API endpoint group files
  Infrastructure/
    Persistence/
      AppDbContext.cs
      DatabaseStartupHelper.cs
      Configurations/ ← 7 IEntityTypeConfiguration<T> classes (one per entity)
    Migrations/     ← 2 EF Core database migrations (consolidated)
  Middleware/       ← Global error handling (ErrorHandlingMiddleware)
  Program.cs        ← Application startup (~80 lines, orchestration only)
  appsettings.json  ← Default configuration
```

### Key Files

| File                                                  | What it does                                                                                                                                        |
| ----------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Program.cs`                                          | Orchestrates startup: loads config, calls `ServiceCollectionExtensions`, builds app, runs `DatabaseStartupHelper`, maps endpoints (~80 lines).      |
| `Configuration/ServiceCollectionExtensions.cs`        | Registers all services, configures database, auth, CORS, Swagger. Contains .env loading and env override helpers.                                   |
| `Infrastructure/Persistence/AppDbContext.cs`          | Entity Framework DbContext with 7 DbSets, `ApplyConfigurationsFromAssembly`, and automatic timestamp management.                                    |
| `Infrastructure/Persistence/DatabaseStartupHelper.cs` | Migration consolidation, admin seeding, and CSV demo import logic extracted from Program.cs.                                                        |
| `Common/HttpContextExtensions.cs`                     | Extension methods on `HttpContext` for `GetUserId()` and `IsAdmin()`.                                                                               |
| `Middleware/ErrorHandlingMiddleware.cs`               | Catches all unhandled exceptions, maps SQLite constraint violations and known exception types to HTTP status codes.                                 |
| `Common/CsvImportHelper.cs`                           | Notion CSV parser + domain import logic for characters, content, and trackings. Also contains enum parsing for difficulties, frequencies, statuses. |

---

## 4. Startup Flow

When the app starts, `Program.cs` delegates to `ServiceCollectionExtensions` and `DatabaseStartupHelper`:

### 1. Load Configuration

```
appsettings.json → appsettings.Development.json → .env file → environment variables
```

The `.env` file is loaded manually (line-by-line parsing). Then, explicit environment variable overrides are applied using `ApplyEnvOverride` helpers:

| Environment Variable                    | Configuration Key                | Purpose                 |
| --------------------------------------- | -------------------------------- | ----------------------- |
| `DATABASE_PATH`                         | `DatabaseSettings:DatabasePath`  | SQLite file location    |
| `JWT_SECRET_KEY`                        | `JwtSettings:SecretKey`          | JWT signing key         |
| `JWT_ISSUER`                            | `JwtSettings:Issuer`             | Token issuer            |
| `JWT_AUDIENCE`                          | `JwtSettings:Audience`           | Token audience          |
| `JWT_ACCESS_TOKEN_MINUTES`              | `JwtSettings:AccessTokenMinutes` | Access token lifetime   |
| `JWT_REFRESH_TOKEN_DAYS`                | `JwtSettings:RefreshTokenDays`   | Refresh token lifetime  |
| `SEED_ADMIN_EMAIL`                      | `SeedSettings:AdminEmail`        | Admin seed email        |
| `SEED_ADMIN_USERNAME`                   | `SeedSettings:AdminUsername`     | Admin seed username     |
| `SEED_ADMIN_PASSWORD`                   | `SeedSettings:AdminPassword`     | Admin seed password     |
| `SEED_ADMIN_ENABLED`                    | `SeedSettings:AdminEnabled`      | Enable admin seeding    |
| `DEMO_IMPORT_ENABLED`                   | `SeedSettings:DemoImportEnabled` | Enable CSV demo import  |
| `CSV_DATA_PATH`                         | `SeedSettings:CsvDataPath`       | Path to CSV files       |
| `CORS_ALLOWED_ORIGINS` / `FRONTEND_URL` | `CorsSettings:AllowedOrigins`    | Comma-separated origins |

### 2. Bind Settings to Options Classes

Four configuration sections are bound to strongly-typed classes:

- `DatabaseSettings` — Database file path, sensitive data logging flag
- `JwtSettings` — Secret key, issuer, audience, access token minutes, refresh token days
- `CorsSettings` — Allowed origins list
- `SeedSettings` — Admin credentials, demo import flag, CSV data path

### 3. Configure Database

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
    if (dbSettings.EnableSensitiveDataLogging)
        options.EnableSensitiveDataLogging();
});
```

The database path is resolved to an absolute path, and the directory is created if it doesn't exist.

### 4. Configure Authentication

JWT Bearer authentication with validation parameters:

- Validates issuer, audience, lifetime, and signing key
- Uses HMAC-SHA256 signing
- Clock skew set to 30 seconds (default is 5 minutes)
- Logs expired tokens as warnings, other auth failures as errors

**Startup validation:** Throws `InvalidOperationException` if `SecretKey` is empty. This prevents the app from starting with an unconfigured JWT secret.

### 5. Configure CORS

- **Production:** Throws if no `AllowedOrigins` are configured
- **Development:** Falls back to `AllowAnyOrigin`
- Two policies: `AllowSpecificOrigins` (with configured origins) and `AllowAll` (development only)

### 6. Register Services

All service registrations are in `Configuration/ServiceCollectionExtensions.cs`:

```csharp
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IWarbandService, WarbandService>();
builder.Services.AddScoped<IUserMotiveService, UserMotiveService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<IResetService, ResetService>();
```

All services are `Scoped` because they depend on `AppDbContext` (which is scoped by default).

### 7. Apply Migrations and Seed

After building the app, `DatabaseStartupHelper` handles:

1. **Migration consolidation** (`ApplyMigrationsAsync`) — Detects legacy multi-migration databases and patches them to the single consolidated migration. For fresh databases, runs `MigrateAsync` normally.
2. **Admin seeding** (`SeedAdminAsync`) — Creates the admin user if `SeedSettings.AdminEnabled` is true. Also creates default warband ("Favourites") and default motives ("Mounts", "Transmog", "Achievement", "Anima", "Reputation", "Toys").
3. **CSV demo import** — If `DemoImportEnabled` is true, imports characters, content, and trackings from Notion-exported CSV files.

### 8. Configure Middleware Pipeline

```
Swagger → ErrorHandling → CORS → Authentication → Authorization → Endpoints
```

### 9. Map Endpoints

```csharp
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
```

Plus a `/health` endpoint with database connectivity check.

---

## 5. Minimal API Pattern

This project uses **Minimal API** instead of MVC controllers. Endpoints are defined as extension methods on `WebApplication`.

### How It Works

Each domain has an endpoint file in `Endpoints/` that defines a static extension method:

```csharp
public static class CharacterEndpoints
{
    public static void MapCharacterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/characters").WithTags("Characters").RequireAuthorization();

        group.MapGet("/", async (ICharacterService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            return Results.Ok(await service.GetAllAsync(userId.Value));
        }).WithName("GetCharacters").WithSummary("List characters for the current user");

        // ... more endpoints
    }
}
```

### Key Differences from Controllers

| Aspect       | Controllers                                | Minimal API (this project)         |
| ------------ | ------------------------------------------ | ---------------------------------- |
| File type    | Class inheriting `ControllerBase`          | Static class with extension method |
| Auth         | `[Authorize]` attribute                    | `.RequireAuthorization()` chain    |
| Route        | `[Route("api/characters")]`                | `app.MapGroup("/characters")`      |
| DI           | Constructor injection                      | Method parameter injection         |
| Return type  | `ActionResult<T>`                          | `IResult` via `Results.Ok()`, etc. |
| User ID      | `User.FindFirst(...)` or base class helper | `ctx.GetUserId()` extension method |
| Swagger tags | `[ApiController]` + conventions            | `.WithTags("Characters")`          |

### Endpoint Group Pattern

Most endpoint files follow this structure:

1. Create a route group with `MapGroup`, tags, and authorization
2. Define each endpoint as a lambda
3. Extract user ID from `HttpContext` via `GetUserId()`
4. Call the service layer
5. Return appropriate `Results.*` response

### Current Endpoint Groups

| File                     | Route Prefix   | Auth                                    | Service Used                     |
| ------------------------ | -------------- | --------------------------------------- | -------------------------------- |
| `AuthEndpoints.cs`       | `/auth`        | Mixed (some anonymous, some authorized) | `IAuthService`                   |
| `AdminEndpoints.cs`      | `/admin`       | Authorized + admin check                | `IAdminService` + `IAuthService` |
| `CharacterEndpoints.cs`  | `/characters`  | Authorized                              | `ICharacterService`              |
| `ContentEndpoints.cs`    | `/contents`    | Authorized                              | `IContentService`                |
| `TrackingEndpoints.cs`   | `/trackings`   | Authorized                              | `ITrackingService`               |
| `DashboardEndpoints.cs`  | `/dashboard`   | Authorized                              | `IDashboardService`              |
| `WarbandEndpoints.cs`    | `/warbands`    | Authorized                              | `IWarbandService`                |
| `UserMotiveEndpoints.cs` | `/motives`     | Authorized                              | `IUserMotiveService`             |
| `DataEndpoints.cs`       | `/admin/data`  | Authorized                              | `IDataService`                   |
| `ResetEndpoints.cs`      | `/admin/reset` | Authorized + admin check                | `IResetService`                  |

---

## 6. Authentication System

### Overview

The project uses JWT Bearer authentication with refresh token rotation:

1. User logs in with email + password → receives access token + refresh token
2. Access token is sent in `Authorization: Bearer <token>` header for every request
3. When the access token expires, the frontend calls `/auth/refresh` with the refresh token
4. The server verifies the refresh token, revokes it, and issues new access + refresh tokens
5. If a revoked refresh token is reused (possible theft), all of the user's tokens are revoked

### Token Generation

**Access Token:**

- JWT with claims: `NameIdentifier` (user ID), `Email`, `Name`, `Role` (if admin)
- Signed with HMAC-SHA256 using the configured secret key
- Expiration: `AccessTokenMinutes` (default: 525600 = 1 year — should be reduced)

**Refresh Token:**

- 64 random bytes, base64-encoded
- Stored as SHA-256 hash in database (not the raw token)
- Expiration: `RefreshTokenDays` (default: 365)
- Includes device name and user agent for session management

### Reuse Detection

If someone tries to use a refresh token that has already been revoked:

```csharp
if (!existing.IsActive)
{
    // Possible token theft — revoke ALL of this user's active tokens
    var allActive = await _context.RefreshTokens
        .Where(rt => rt.UserId == existing.UserId && rt.RevokedAt == null)
        .ToListAsync();
    foreach (var t in allActive)
        t.RevokedAt = DateTime.UtcNow;
}
```

This is a security feature that invalidates all sessions if token theft is detected.

### User ID Extraction

Every authenticated endpoint gets the current user ID via:

```csharp
var userId = ctx.GetUserId();
if (userId == null) return Results.Unauthorized();
```

This uses the `HttpContextExtensions` extension method:

```csharp
public static Guid? GetUserId(this HttpContext context)
{
    var claim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Guid.TryParse(claim, out var id) ? id : null;
}
```

### Admin Check

Admin-only endpoints use:

```csharp
if (!ctx.IsAdmin()) return Results.Forbid();
```

Which checks the `Role` claim:

```csharp
public static bool IsAdmin(this HttpContext context) =>
    context.User?.IsInRole("Admin") ?? false;
```

---

## 7. Database Schema

### Entity Relationship Diagram

```
User (Guid Id)
├── RefreshToken[] (Guid Id, FK UserId) — CASCADE delete
├── Warband[] (Guid Id, FK OwnerUserId) — CASCADE delete
│   └── Character[] (FK WarbandId) — SET NULL on warband delete
├── UserMotive[] (Guid Id, FK OwnerUserId) — CASCADE delete
│   └── Content[] (many-to-many via ContentUserMotives)
└── (no direct nav to Characters or Contents)

Character (Guid Id)
├── FK OwnerUserId → User (SET NULL) — nullable
├── FK WarbandId → Warband (SET NULL) — nullable
└── Tracking[] (FK CharacterId) — CASCADE delete

Content (Guid Id)
├── FK OwnerUserId → User (SET NULL) — nullable
├── UserMotive[] (many-to-many via ContentUserMotives)
└── Tracking[] (FK ContentId) — CASCADE delete

Tracking (Guid Id)
├── FK CharacterId → Character (CASCADE)
├── FK ContentId → Content (CASCADE)
└── Unique index on (CharacterId, ContentId, Difficulty)
```

### All Entities

| Entity         | Table         | PK Type | Key Properties                                                                                                                          |
| -------------- | ------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `User`         | Users         | Guid    | Email (unique), UserName, PasswordHash, IsAdmin, IsActive                                                                               |
| `RefreshToken` | RefreshTokens | Guid    | TokenHash (unique), UserId (FK), ExpiresAt, RevokedAt, DeviceName                                                                       |
| `Warband`      | Warbands      | Guid    | Name, Color, SortOrder, OwnerUserId (FK). Unique: (OwnerUserId, Name)                                                                   |
| `UserMotive`   | UserMotives   | Guid    | Name, Color, OwnerUserId (FK). Unique: (OwnerUserId, Name)                                                                              |
| `Character`    | Characters    | Guid    | Name, Class, Race, Level, Covenant, WarbandId (FK), OwnerUserId (FK)                                                                    |
| `Content`      | Contents      | Guid    | Name, Expansion, AllowedDifficulties (bitmask), Comment, OwnerUserId (FK)                                                               |
| `Tracking`     | Trackings     | Guid    | CharacterId (FK), ContentId (FK), Difficulty, Frequency, Status, Comment, LastCompletedAt. Unique: (CharacterId, ContentId, Difficulty) |

### Enums

**DifficultyFlags** (bitmask, `[Flags]`):

- `None = 0`, `LFR = 1`, `Normal = 2`, `Heroic = 4`, `Mythic = 8`
- Used as bitmask in `Content.AllowedDifficulties` (e.g., `Normal|Heroic|Mythic = 14`)
- Used as single flag in `Tracking.Difficulty` (e.g., `Heroic = 4`)

**Frequency:**

- `Hourly = 0`, `Daily = 1`, `Weekly = 2`, `Monthly = 3`

**TrackingStatus:**

- `NotStarted = 0`, `Pending = 1`, `InProgress = 2`, `LastDay = 3`, `LastWeek = 4`, `Finished = 5`

### Timestamp Management

All entities have `CreatedAt` and `UpdatedAt` (except `RefreshToken` which only has `CreatedAt`). These are set automatically in `AppDbContext`:

```csharp
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    UpdateTimestamps();
    return base.SaveChangesAsync(cancellationToken);
}
```

The `UpdateTimestamps` method uses `ChangeTracker.Entries()` to detect Added/Modified entities and set timestamps. On modify, `CreatedAt` is explicitly marked as not modified to prevent accidental overwrites.

---

## 8. Service Layer

### Pattern

Every domain has a service with an interface:

```
IAuthService       → AuthService
ICharacterService  → CharacterService
IContentService    → ContentService
ITrackingService   → TrackingService
IDashboardService  → DashboardService
IWarbandService    → WarbandService
IUserMotiveService → UserMotiveService
IAdminService      → AdminService
IDataService       → DataService
IResetService      → ResetService
```

Each service:

- Receives `AppDbContext` via constructor injection
- Contains all business logic for its domain
- Maps entities to DTOs before returning
- Does NOT know about HTTP (`HttpContext`, status codes, etc.)
- Returns typed results (DTOs, tuples with error strings, or `null` for not found)

### Error Handling Pattern

Services use different patterns depending on the operation:

**Simple CRUD (return null for not found):**

```csharp
public async Task<CharacterDto?> GetByIdAsync(Guid id)
{
    var c = await _context.Characters.FindAsync(id);
    return c == null ? null : ToDto(c);
}
```

**Operations with validation (return tuple):**

```csharp
public async Task<(TrackingDto? Dto, string? Error)> CreateAsync(...)
{
    if (character == null) return (null, "Character not found.");
    if (exists) return (null, "A tracking entry already exists.");
    // ... create and return (dto, null)
}
```

**Delete (return bool):**

```csharp
public async Task<bool> DeleteAsync(Guid id)
{
    var entity = await _context.Find(id);
    if (entity == null) return false;
    _context.Remove(entity);
    await _context.SaveChangesAsync();
    return true;
}
```

### Mapping

Each service has a private static `ToDto` method that maps entities to DTOs:

```csharp
private static CharacterDto ToDto(Character c) => new(
    c.Id, c.Name, c.Level, c.Class, c.Race, c.Covenant,
    c.WarbandId, c.Warband?.Name, c.Warband?.Color,
    c.OwnerUserId, c.OwnerUser?.UserName,
    c.CreatedAt, c.UpdatedAt);
```

This is simple and works well for the project's size.

---

## 9. How to Add a New Feature

### Example: Adding a "Notes" field to Characters

#### Step 1: Update the Entity

```csharp
// Domain/Entities/Warcraft/Character.cs
public string? Notes { get; set; }
```

#### Step 2: Update the DTOs

```csharp
// Contracts/CharacterContracts.cs
public record CreateCharacterRequest(
    string Name, int? Level, string Class, string? Race,
    string? Covenant, Guid? WarbandId, Guid? OwnerUserId,
    string? Notes);  // ← add

public record UpdateCharacterRequest(
    string Name, int? Level, string Class, string? Race,
    string? Covenant, Guid? WarbandId, Guid? OwnerUserId,
    string? Notes);  // ← add

public record CharacterDto(
    Guid Id, string Name, int? Level, string Class, string? Race,
    string? Covenant, Guid? WarbandId, string? WarbandName, string? WarbandColor,
    Guid? OwnerUserId, string? OwnerUserName,
    DateTime CreatedAt, DateTime UpdatedAt,
    string? Notes);  // ← add
```

#### Step 3: Update the Service

```csharp
// Application/Services/CharacterService.cs — in CreateAsync
var character = new Character
{
    Name = request.Name.Trim(),
    // ... existing fields
    Notes = request.Notes?.Trim(),  // ← add
};

// In UpdateAsync
character.Notes = request.Notes?.Trim();  // ← add

// In ToDto
private static CharacterDto ToDto(Character c) => new(
    c.Id, c.Name, c.Level, c.Class, c.Race, c.Covenant,
    c.WarbandId, c.Warband?.Name, c.Warband?.Color,
    c.OwnerUserId, c.OwnerUser?.UserName,
    c.CreatedAt, c.UpdatedAt,
    c.Notes);  // ← add
```

#### Step 4: Update the DbContext (if needed)

If the new field needs constraints, add an `IEntityTypeConfiguration<T>` or update the existing one:

```csharp
// Infrastructure/Persistence/Configurations/CharacterConfiguration.cs
e.Property(c => c.Notes).HasMaxLength(2000);
```

#### Step 5: Create a Migration

```bash
cd WarcraftArchive/WarcraftArchive.Api
dotnet ef migrations add AddCharacterNotes
```

#### Step 6: Update the Frontend

Add `notes` to the TypeScript `Character` interface in `src/models/api/Character.ts` and update the form/display components.

#### Step 7: Test

```bash
dotnet run
# Test via Swagger:
# POST /characters with notes field
# GET /characters — verify notes returned
# PUT /characters/{id} — verify notes updated
```

---

## 10. How to Add a New Service

### Example: Adding a ResetService

#### Step 1: Create the Interface

```csharp
// Application/Interfaces/IResetService.cs
namespace WarcraftArchive.Api.Application.Interfaces;

public interface IResetService
{
    Task<int> ApplyDailyResetAsync();
    Task<(int Weekly, int Daily)> ApplyWeeklyResetAsync();
}
```

#### Step 2: Create the Implementation

```csharp
// Application/Services/ResetService.cs
using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Infrastructure.Persistence;
using WarcraftArchive.Api.Domain.Enums;

namespace WarcraftArchive.Api.Application.Services;

public class ResetService : IResetService
{
    private readonly AppDbContext _context;
    public ResetService(AppDbContext context) => _context = context;

    public async Task<int> ApplyDailyResetAsync()
    {
        var trackings = await _context.Trackings
            .Where(t => t.Frequency == Frequency.Daily &&
                        (t.Status == TrackingStatus.Finished ||
                         t.Status == TrackingStatus.LastDay ||
                         t.Status == TrackingStatus.InProgress ||
                         t.Status == TrackingStatus.Pending))
            .ToListAsync();

        foreach (var t in trackings)
            t.Status = t.Status == TrackingStatus.Finished
                ? TrackingStatus.LastDay
                : TrackingStatus.NotStarted;

        if (trackings.Count > 0)
            await _context.SaveChangesAsync();

        return trackings.Count;
    }

    public async Task<(int Weekly, int Daily)> ApplyWeeklyResetAsync()
    {
        // Weekly reset
        var weeklyTrackings = await _context.Trackings
            .Where(t => t.Frequency == Frequency.Weekly &&
                        (t.Status == TrackingStatus.Finished ||
                         t.Status == TrackingStatus.LastWeek ||
                         t.Status == TrackingStatus.InProgress ||
                         t.Status == TrackingStatus.Pending))
            .ToListAsync();

        foreach (var t in weeklyTrackings)
            t.Status = t.Status == TrackingStatus.Finished
                ? TrackingStatus.LastWeek
                : TrackingStatus.NotStarted;

        if (weeklyTrackings.Count > 0)
            await _context.SaveChangesAsync();

        // Also apply daily reset
        var dailyCount = await ApplyDailyResetAsync();

        return (weeklyTrackings.Count, dailyCount);
    }
}
```

#### Step 3: Register in DI

```csharp
// Configuration/ServiceCollectionExtensions.cs — in the services section
builder.Services.AddScoped<IResetService, ResetService>();
```

#### Step 4: Update the Endpoint

```csharp
// Endpoints/ResetEndpoints.cs — replace direct DbContext usage
group.MapPost("/daily", async (HttpContext ctx, IResetService resetService) =>
{
    if (!ctx.IsAdmin()) return Results.Forbid();
    var affected = await resetService.ApplyDailyResetAsync();
    return Results.Ok(new { affected, message = $"Daily reset applied. {affected} tracking(s) updated." });
});
```

---

## 11. How the Tracking System Works

The tracking system is the core business logic of WarcraftArchive.

### Concepts

- **Character** — A WoW character owned by a user, optionally in a warband
- **Content** — A farmable instance (raid, dungeon) with an expansion and allowed difficulties
- **Tracking** — A unique (Character × Content × Difficulty) combination with status and frequency
- **Motive** — A tag/goal attached to content (e.g., "this raid is farmed for Mounts and Transmog")

### Tracking Lifecycle

```
NotStarted → Pending → InProgress → Finished
                                        ↓
                           (Daily Reset) → LastDay → NotStarted
                           (Weekly Reset) → LastWeek → NotStarted
```

### Reset State Machine

**Daily reset** (for `Frequency.Daily` trackings):

- `Finished` → `LastDay` (preserves "completed yesterday" info)
- `LastDay`, `InProgress`, `Pending` → `NotStarted`

**Weekly reset** (for `Frequency.Weekly` trackings):

- `Finished` → `LastWeek` (preserves "completed last week" info)
- `LastWeek`, `InProgress`, `Pending` → `NotStarted`
- A weekly reset also triggers a daily reset

### Difficulty Validation

When creating a tracking, the service validates that the requested difficulty is allowed by the content:

```csharp
if ((content.AllowedDifficulties & (int)request.Difficulty) == 0)
    return (null, $"Difficulty '{request.Difficulty}' is not allowed for this content.");
```

### Duplicate Prevention

Each (Character, Content, Difficulty) combination must be unique:

```csharp
var exists = await _context.Trackings.AnyAsync(t =>
    t.CharacterId == request.CharacterId &&
    t.ContentId == request.ContentId &&
    t.Difficulty == request.Difficulty);
if (exists) return (null, "A tracking entry already exists.");
```

This is also enforced by a unique composite index in the database.

---

## 12. How the Dashboard Works

The dashboard provides a weekly tracking summary:

```csharp
public async Task<WeeklyDashboardDto> GetWeeklyAsync(Guid ownerUserId)
{
    var items = await _context.Trackings
        .Include(t => t.Character)
        .Include(t => t.Content).ThenInclude(c => c.Motives)
        .Where(t => t.Frequency == Frequency.Weekly && t.Character.OwnerUserId == ownerUserId)
        .OrderBy(t => t.Status)
        .ThenBy(t => t.Content.Expansion)
        .ThenBy(t => t.Content.Name)
        .ToListAsync();

    return new WeeklyDashboardDto(
        Total: items.Count,
        NotStarted: items.Count(t => t.Status == TrackingStatus.NotStarted),
        Pending: items.Count(t => t.Status == TrackingStatus.Pending),
        InProgress: items.Count(t => t.Status == TrackingStatus.InProgress),
        LastDay: items.Count(t => t.Status == TrackingStatus.LastDay),
        LastWeek: items.Count(t => t.Status == TrackingStatus.LastWeek),
        Finished: items.Count(t => t.Status == TrackingStatus.Finished),
        Items: items.Select(TrackingService.ToDto).ToList());
}
```

The frontend uses the counts for progress bars and the items list for the detailed grid.

---

## 13. How the Admin System Works

### User Management

Admin endpoints delegate to `IAuthService`:

- Create user → `authService.CreateUserAsync()` (also seeds default warband + motives)
- Update user → `authService.UpdateUserAsync()` (can toggle admin/active status)
- Delete user → `authService.DeleteUserAsync()` (cascades: trackings → characters → contents → tokens)

### Orphan Management

When a user is deleted, characters and content with nullable `OwnerUserId` may become "orphaned" (their FK is set to null via `SetNull` delete behavior). The admin endpoints provide:

- **List orphans** — characters, contents, and trackings without an owner
- **Claim orphans** — assign orphaned records to a specific user
- **Delete orphans** — remove orphaned records individually or in bulk

Currently, orphan management is handled by `IAdminService`.

---

## 14. How the CSV Import/Export Works

### Export

`DataService` generates CSV files by querying the database and building CSV strings with `StringBuilder`:

```csharp
var sb = new StringBuilder();
sb.AppendLine("Name,Class,Race,Level,Covenant,Warband");
foreach (var c in characters)
    sb.AppendLine($"{Csv(c.Name)},{Csv(c.Class)},{Csv(c.Race)},{c.Level},{Csv(c.Covenant)},{Csv(c.Warband?.Name)}");
return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "characters.csv");
```

Export endpoints: `/admin/data/export/characters`, `/admin/data/export/content`, `/admin/data/export/progress`

### Import

Import endpoints read the request body as raw text, parse it with `CsvImportHelper.ParseCsvText`, then process each row with validation:

- **Characters:** Validates class (13 WoW classes), race (26 races), level (1-80). Auto-creates warbands.
- **Content:** Validates difficulties (must parse to valid flags). Auto-creates motives.
- **Progress:** Looks up existing characters and content by name. Validates difficulty against content's allowed difficulties.

Import is idempotent — duplicates are counted but not re-inserted.

Import endpoints: `/admin/data/import/characters`, `/admin/data/import/content`, `/admin/data/import/progress`

### Startup CSV Import

Separate from the admin import endpoints, `CsvImportHelper.ImportAsync` runs on startup when `DemoImportEnabled` is true. It reads files from `CsvDataPath` using pattern matching (looks for filenames containing "Personajes", "Raids", "Content Progress"). This is designed for one-time migration from Notion.

---

## 15. Configuration Deep Dive

### DatabaseSettings

```csharp
public class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";
    public string DatabasePath { get; set; } = "/data/warcraftarchive.db";
    public bool EnableSensitiveDataLogging { get; set; } = false;
}
```

- **Docker default:** `/data/warcraftarchive.db` (mounted volume)
- **Dev default:** `../warcraftarchive-dev.db` (from `appsettings.Development.json`)
- Override: `DATABASE_PATH` environment variable

### JwtSettings

```csharp
public class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "WarcraftArchive.Api";
    public string Audience { get; set; } = "WarcraftArchive.Client";
    public int AccessTokenMinutes { get; set; } = 525600; // 1 year
    public int RefreshTokenDays { get; set; } = 365;
}
```

- **SecretKey:** Empty by default — must be set via `JWT_SECRET_KEY` or appsettings. App throws on startup if empty.
- **Dev key:** `"DevOnlySecretKeyThatIsAtLeast32CharactersLong!"` (from `appsettings.Development.json`)
- **AccessTokenMinutes:** Default is 525600 (1 year) — effectively never expires. Should be reduced once refresh flow is confirmed working.

### SeedSettings

```csharp
public class SeedSettings
{
    public const string SectionName = "SeedSettings";
    public bool AdminEnabled { get; set; } = false;
    public string AdminEmail { get; set; } = "admin@local";
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "Admin123!123!";
    public bool DemoImportEnabled { get; set; } = false;
    public string CsvDataPath { get; set; } = "/data/csv";
}
```

- **AdminEnabled:** Disabled by default. Enable with `SEED_ADMIN_ENABLED=true`.
- **AdminPassword:** Has a default — should be overridden in production via `SEED_ADMIN_PASSWORD`.
- **DemoImportEnabled:** One-time Notion import. Enable with `DEMO_IMPORT_ENABLED=true`.

---

## 16. Error Handling

### Global Middleware

`ErrorHandlingMiddleware` catches all unhandled exceptions and maps them to HTTP responses:

| Exception Type                                       | HTTP Status               | Message                                       |
| ---------------------------------------------------- | ------------------------- | --------------------------------------------- |
| `DbUpdateException` with `SqliteException` (code 19) | 409 Conflict              | "Conflict: duplicate or constraint violation" |
| `DbUpdateException` (other)                          | 400 Bad Request           | "Error saving data"                           |
| `ArgumentException`                                  | 400 Bad Request           | "Invalid data"                                |
| `UnauthorizedAccessException`                        | 403 Forbidden             | "Access denied"                               |
| `KeyNotFoundException`                               | 404 Not Found             | "Resource not found"                          |
| All others                                           | 500 Internal Server Error | "An unexpected error occurred"                |

Response shape: `{ statusCode, message, details? }` (details are null for 500 errors).

### Service-Level Error Handling

Services do NOT throw exceptions for business errors. They return:

- `null` for "not found"
- `(null, "error message")` tuple for validation errors
- `false` for "could not delete"

Endpoints translate these into HTTP status codes.

---

## 17. Frontend Integration

### How the Frontend Calls the Backend

The React frontend uses a custom `fetch` wrapper (`customFetch.ts`) that:

1. Reads the access token from the Redux store
2. Sets `Authorization: Bearer <token>` header
3. On 401 response, automatically calls `/auth/refresh` with the refresh token
4. If refresh succeeds, retries the original request
5. If refresh fails, forces logout (clears Redux, redirects to `/login`)

### API Routes

All endpoint URLs are centralized in `src/environments/apiRoutes.ts`:

```typescript
const apiRoutes = {
  health: "/health",
  auth: {
    login: "/auth/login",
    refresh: "/auth/refresh",
    logout: "/auth/logout",
    me: "/auth/me",
    logoutAll: "/auth/logout-all",
  },
  characters: {
    base: "/characters",
    byId: (id: string) => `/characters/${id}`,
  },
  // ... etc
};
```

### Service Layer

Each domain has a TypeScript service file (e.g., `characterService.ts`):

```typescript
export const characterService = {
  getAll: () => customFetch<Character[]>(apiRoutes.characters.base),
  getById: (id: string) =>
    customFetch<Character>(apiRoutes.characters.byId(id)),
  create: (data: CreateCharacterRequest) =>
    customFetch<Character>(apiRoutes.characters.base, {
      method: "POST",
      body: data,
    }),
  update: (id: string, data: UpdateCharacterRequest) =>
    customFetch<Character>(apiRoutes.characters.byId(id), {
      method: "PUT",
      body: data,
    }),
  delete: (id: string) =>
    customFetch<void>(apiRoutes.characters.byId(id), { method: "DELETE" }),
};
```

### Contract Mapping

| Backend C# Record    | Frontend TypeScript | Notes                                     |
| -------------------- | ------------------- | ----------------------------------------- |
| `CharacterDto`       | `Character`         | camelCase serialization matches           |
| `ContentDto`         | `Content`           | Includes `motives` array                  |
| `TrackingDto`        | `Tracking`          | Includes flattened character/content info |
| `WarbandDto`         | `Warband`           |                                           |
| `UserMotiveDto`      | `UserMotive`        |                                           |
| `WeeklyDashboardDto` | `WeeklyDashboard`   | Powers home screen                        |
| `LoginResponse`      | `LoginResponse`     | Includes both tokens                      |

---

## 18. Common Development Tasks

### Run the Backend

```bash
cd WarcraftArchive/WarcraftArchive.Api
dotnet run
# API available at https://localhost:7178 (or configured port)
# Swagger at https://localhost:7178/swagger
```

### Add a Migration

```bash
cd WarcraftArchive/WarcraftArchive.Api
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### Verify No Pending Model Changes

```bash
dotnet ef migrations has-pending-model-changes
```

### Reset the Dev Database

Delete `warcraftarchive-dev.db` and restart the app. Migrations will recreate it, and seeding will populate it if `AdminEnabled` is true.

### Test an Endpoint via Swagger

1. Start the app
2. Go to `/swagger`
3. Click "Authorize" and paste a JWT token (get one from POST `/auth/login`)
4. Try the endpoint

### Check the Health Endpoint

```
GET /health
```

Returns: `{ status: "healthy", version: "1.0.0", db: "ok", timestamp: "..." }`

---

## 19. Key Design Decisions

### Why Minimal API instead of Controllers?

Minimal API provides the same functionality with less ceremony. For a project this size (~30 endpoints), the endpoint group pattern is cleaner than controllers:

- No base class needed
- No attributes
- Route grouping with `MapGroup` replaces `[Route]`
- Method-level auth replaces class-level `[Authorize]`

### Why Guid IDs instead of int?

- No auto-increment collisions in distributed or imported data
- Client-generated IDs possible (not used here, but enables it)
- Harder to enumerate (security benefit)
- Default in `Guid.NewGuid()` — no database roundtrip needed to know the ID

### Why separate Auth and Warcraft model namespaces?

Entities are split into `Domain.Entities.Auth` (User, RefreshToken, Warband, UserMotive) and `Domain.Entities.Warcraft` (Character, Content, Tracking) to reflect two different domain concerns:

- Auth models are about identity, sessions, and user-owned configurations
- Warcraft models are about the game domain

Warbands and UserMotives are in `Auth` because they are user-owned configuration, even though they relate to Warcraft gameplay.

### Why refresh token rotation?

Without rotation, a stolen refresh token gives permanent access. With rotation:

- Each refresh token is single-use
- Reuse detection triggers full session invalidation
- The attacker's stolen token becomes invalid after the legitimate user refreshes

### Why bitmask for difficulties?

A bitmask stores multiple difficulty flags in a single integer:

- `Normal|Heroic = 2|4 = 6`
- Check: `(allowedDifficulties & (int)difficulty) != 0`
- Compact storage, fast bitwise operations
- Works well with SQLite (stored as integer)
