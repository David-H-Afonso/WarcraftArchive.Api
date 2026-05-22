# Backend Refactor Plan — WarcraftArchive API

## Implementation Summary

**Phases 0–9 have been completed.** The backend has been fully refactored into the target architecture:

- Program.cs reduced from ~300 to ~80 lines, with startup logic extracted to `ServiceCollectionExtensions` and `DatabaseStartupHelper`
- Entity configurations split into 7 `IEntityTypeConfiguration<T>` classes under `Infrastructure/Persistence/Configurations/`
- Three new services created: `AdminService`, `DataService`, `ResetService` — all endpoint files now delegate to the service layer
- Folder structure reorganized: `Domain/Entities/`, `Domain/Enums/`, `Application/Interfaces/`, `Application/Services/`, `Infrastructure/Persistence/`, `Contracts/`, `Common/`
- Contracts reorganized from flat `DTOs/*.cs` files into domain-grouped files under `Contracts/`
- Security hardening applied: `OwnerUserId` removed from `UpdateCharacterRequest`, ownership checks added
- Dead code removed (`MotiveFlags` enum), files renamed (`HttpContextExtensions`)
- **Phase 10 (Tests) remains not started**

---

## 1. Executive Summary

WarcraftArchive is an ASP.NET Core 9 Minimal API that tracks World of Warcraft farming progress — characters, raid/dungeon content, difficulty-based tracking entries, and weekly/daily reset cycles. It provides a React frontend for managing warbands, motives, and farming dashboards.

**Technology stack:** .NET 9, ASP.NET Core Minimal API, Entity Framework Core 9 with SQLite, JWT authentication with refresh token rotation, Swagger/OpenAPI, BCrypt for password hashing, Docker-ready.

**Current architectural quality:** Good overall. This is the most architecturally mature project in the portfolio. It already uses Minimal API with proper service layer (all 7 services have interfaces), typed configuration classes, organized DTOs, domain models split by subdomain (Auth vs Warcraft), global error handling, and secure startup validation. The codebase works well and would pass a basic code review with relatively minor corrections.

**Main problems:**

- `Program.cs` is ~300 lines with migration consolidation logic, admin seeding, CSV import, and local helper functions that should be extracted.
- `AdminEndpoints.cs` (~180 lines) and `DataEndpoints.cs` (~330 lines) are fat endpoint files that inject `AppDbContext` directly, bypassing the service layer.
- `ResetEndpoints.cs` injects `AppDbContext` directly for reset logic that should be in a service.
- `OnModelCreating` (~90 lines) is manageable but should be split into `IEntityTypeConfiguration<T>` classes for consistency with the standard.
- `CsvImportHelper.cs` (~300 lines) is a large utility mixing parsing, importing, and enum mapping.
- No test coverage exists.
- `MotiveFlags` enum exists but is unused — motives are now stored as a many-to-many relationship.

**Main opportunities:**

- The service layer pattern is already in place for all core domains. Extending it to admin, data, and reset operations is straightforward.
- The Minimal API endpoint pattern with extension methods is clean and consistent.
- The frontend is well-structured with typed services and centralized API routes, making API contract preservation easy.
- The migration consolidation logic is correct and well-documented — it just needs to be moved.

**Recommended direction:** Progressive refactoring in phases, extracting service layers from fat endpoint files, breaking up `Program.cs`, splitting entity configurations, and reorganizing contracts. The existing architecture is solid — this is refinement, not restructuring.

---

## 2. Current Backend Structure

```
WarcraftArchive.Api/
  Configuration/
    CorsSettings.cs
    DatabaseSettings.cs
    JwtSettings.cs
    SeedSettings.cs
  Data/
    AppDbContext.cs
  DTOs/
    AuthDTOs.cs
    CharacterDTOs.cs
    ContentDTOs.cs
    TrackingDTOs.cs
    UserMotiveDTOs.cs
    WarbandDTOs.cs
  Endpoints/
    AdminEndpoints.cs
    AuthEndpoints.cs
    CharacterEndpoints.cs
    ContentEndpoints.cs
    DashboardEndpoints.cs
    DataEndpoints.cs
    ResetEndpoints.cs
    TrackingEndpoints.cs
    UserMotiveEndpoints.cs
    WarbandEndpoints.cs
  Helpers/
    CsvImportHelper.cs
    HttpContextHelper.cs
  Middleware/
    ErrorHandlingMiddleware.cs
  Migrations/
    20260226000000_InitialCreate.cs
    20260226000001_AddWarbandSortOrder.cs
    AppDbContextModelSnapshot.cs
  Models/
    Auth/
      RefreshToken.cs
      User.cs
      UserMotive.cs
      Warband.cs
    Warcraft/
      Character.cs
      Content.cs
      Tracking.cs
      WarcraftEnums.cs
  Services/
    AuthService.cs       + IAuthService.cs
    CharacterService.cs  + ICharacterService.cs
    ContentService.cs    + IContentService.cs
    DashboardService.cs  + IDashboardService.cs
    TrackingService.cs   + ITrackingService.cs
    UserMotiveService.cs + IUserMotiveService.cs
    WarbandService.cs    + IWarbandService.cs
  Program.cs              (~300 lines)
  appsettings.json
  appsettings.Development.json
  Dockerfile
```

**What each folder currently does:**

| Folder           | Purpose                                                                                                                                                 |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Configuration/` | 4 strongly-typed Options classes. Well-implemented with `SectionName` constants.                                                                        |
| `Data/`          | Contains `AppDbContext.cs` (~140 lines). Timestamp management in `SaveChanges`/`SaveChangesAsync`. Entity configuration in `OnModelCreating`.           |
| `DTOs/`          | 6 files containing ~30 DTOs grouped by domain. Records used throughout.                                                                                 |
| `Endpoints/`     | 10 Minimal API endpoint group files. Mix of thin (DashboardEndpoints at ~15 lines) and fat (DataEndpoints at ~330 lines, AdminEndpoints at ~180 lines). |
| `Helpers/`       | 2 utility files: `HttpContextHelper` (extension methods) and `CsvImportHelper` (~300 lines, CSV parsing + import + enum conversion).                    |
| `Middleware/`    | Global error handler with typed exception mapping. Well-implemented.                                                                                    |
| `Migrations/`    | 2 EF Core migrations (consolidated initial + warband sort order).                                                                                       |
| `Models/`        | 8 entity classes split into `Auth/` and `Warcraft/` subdomains. Clean, with navigation properties.                                                      |
| `Services/`      | 14 files (7 interfaces + 7 implementations). All core domain services.                                                                                  |

---

## 3. Current Request Flow

The typical request flow is consistent for most domains:

**For domains with services** (characters, content, tracking, dashboard, warbands, motives, auth):

```
Frontend → Endpoint → Service (via interface) → DbContext → SQLite
         ← Endpoint ← Service returns DTO ← mapped from entities
```

**For domains without services** (admin orphans, data import/export, resets):

```
Frontend → Endpoint → DbContext directly (LINQ queries) → SQLite
         ← Endpoint builds response inline ← maps entity to anonymous/DTO object
```

The second pattern exists in `AdminEndpoints.cs` (orphan management), `DataEndpoints.cs` (CSV import/export), and `ResetEndpoints.cs` (daily/weekly reset). These bypass the service layer and inject `AppDbContext` directly.

---

## 4. Current Database and EF Core Setup

### DbContext

- **Location:** `Data/AppDbContext.cs` (~140 lines)
- **DbSets:** 7 (Users, RefreshTokens, Warbands, UserMotives, Characters, Contents, Trackings)
- **Timestamp handling:** Automatic `CreatedAt`/`UpdatedAt` management in overridden `SaveChanges`/`SaveChangesAsync` using switch-based pattern matching
- **Lazy loading:** Explicitly disabled (`ChangeTracker.LazyLoadingEnabled = false`)

### SQLite Connection

- **Connection string:** Built from `DatabaseSettings.DatabasePath` (default: `/data/warcraftarchive.db`)
- **Path resolution:** Relative paths resolved to absolute via `Path.GetFullPath`
- **Directory creation:** Ensures the database directory exists before connection
- **Environment override:** `DATABASE_PATH` environment variable

### Migrations

Two migrations exist:

1. `20260226000000_InitialCreate` — Consolidated schema (squashed from earlier multi-migration history)
2. `20260226000001_AddWarbandSortOrder` — Added `SortOrder` column to `Warbands`

### Database Initialization

`Program.cs` contains a migration consolidation system (`ApplyMigrationsAsync`, ~60 lines) that handles:

- **Fresh installs:** Runs the consolidated migration normally
- **Legacy databases:** Detects old migration IDs in `__EFMigrationsHistory`, patches missing schema changes (e.g., `OwnerUserId` on `Contents`), and rewrites migration history to the consolidated migration

This logic is correct and necessary for existing deployments, but its location in `Program.cs` adds noise to the startup configuration.

### Admin Seeding

`Program.cs` also contains `SeedAdminAsync` (~20 lines) and `SeedDefaultUserDataAsync` (~25 lines) for creating the initial admin user with default warbands and motives. This seeding logic is duplicated in `AuthService.CreateUserAsync` (which also seeds defaults for new users).

### Entity Configuration

`OnModelCreating` (~90 lines) configures all 7 entities with:

- Primary keys (all `Guid`)
- Unique indexes (Email, TokenHash, composite indexes on owner+name)
- Max lengths and required constraints
- Relationships with explicit delete behaviors (`Cascade` for owned data, `SetNull` for optional references)
- A many-to-many relationship for `Content ↔ UserMotive` via `ContentUserMotives` join table

### Risks

- The duplicated seeding logic (Program.cs + AuthService) means changes must be applied in two places.
- `OnModelCreating` is manageable at ~90 lines but should follow the standard `IEntityTypeConfiguration<T>` pattern for consistency.

---

## 5. Current API Surface

### Authentication & User Management

| Endpoint Group   | Route Prefix | Endpoints                                                                     | Responsibility                                                                           |
| ---------------- | ------------ | ----------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| `AuthEndpoints`  | `/auth`      | POST `/login`, POST `/refresh`, POST `/logout`, GET `/me`, POST `/logout-all` | JWT authentication with refresh token rotation, session info                             |
| `AdminEndpoints` | `/admin`     | 12 endpoints                                                                  | User CRUD, orphan management (list/claim/delete orphaned characters, content, trackings) |

**Concerns:** Auth endpoints are properly thin. `AdminEndpoints` is a fat endpoint file — user CRUD delegates to `AuthService`, but orphan management (7 endpoints) injects `AppDbContext` directly with inline queries and anonymous object responses.

### Core Domain

| Endpoint Group        | Route Prefix  | Endpoints                 | Responsibility                                                                                        |
| --------------------- | ------------- | ------------------------- | ----------------------------------------------------------------------------------------------------- |
| `CharacterEndpoints`  | `/characters` | 5 (CRUD + list)           | Character management for the current user                                                             |
| `ContentEndpoints`    | `/contents`   | 5 (CRUD + list)           | Content/instance management with expansion filter                                                     |
| `TrackingEndpoints`   | `/trackings`  | 5 (CRUD + list)           | Tracking entries with multi-filter support (character, status, frequency, expansion, motive, content) |
| `DashboardEndpoints`  | `/dashboard`  | 1 (GET `/weekly`)         | Aggregated weekly tracking summary                                                                    |
| `WarbandEndpoints`    | `/warbands`   | 6 (CRUD + list + reorder) | Warband management with sort ordering                                                                 |
| `UserMotiveEndpoints` | `/motives`    | 5 (CRUD + list)           | Motive/goal management                                                                                |

**Concerns:** All core domain endpoint files are properly thin — they delegate to services, validate inputs, and return appropriate HTTP status codes. This is the target pattern.

### Data & Administration

| Endpoint Group   | Route Prefix   | Endpoints                         | Responsibility                                                                          |
| ---------------- | -------------- | --------------------------------- | --------------------------------------------------------------------------------------- |
| `DataEndpoints`  | `/admin/data`  | 6 endpoints                       | CSV export (characters, content, progress) + CSV import (characters, content, progress) |
| `ResetEndpoints` | `/admin/reset` | 2 (POST `/daily`, POST `/weekly`) | Force daily/weekly tracking status resets                                               |

**Concerns:** `DataEndpoints` (~330 lines) is the largest endpoint file. It contains CSV generation, validation (class/race lists), parsing, warband/motive auto-creation, and import logic — all inline. Should extract to a `DataService`. `ResetEndpoints` contains reset state machine logic that should be in a `ResetService`.

### System

| Endpoint     | Route     | Responsibility                                     |
| ------------ | --------- | -------------------------------------------------- |
| Health check | `/health` | DB connectivity check, version, pending migrations |

**Concerns:** None — properly implemented with anonymous access.

---

## 6. Current Problems and Risks

| Area                     | Problem                                                                                  | Why it matters                                                                                                          | Severity   | Recommended action                                                                                |
| ------------------------ | ---------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------------------------------- |
| Program.cs               | ~300 lines with migration logic, seeding, CSV import orchestration, and helper functions | Makes startup code harder to read. Migration logic and seeding should be extracted.                                     | **Medium** | Extract migration helpers and seeding to dedicated classes. Create `ServiceCollectionExtensions`. |
| DataEndpoints            | ~330 lines with CSV export, import, validation, and auto-creation logic                  | Violates thin endpoint principle. Contains inline WoW class/race validation, CSV parsing, warband/motive auto-creation. | **High**   | Extract to `IDataService` / `DataService`                                                         |
| AdminEndpoints (orphans) | 7 orphan-management endpoints inject DbContext directly                                  | Bypasses service layer. Inline queries with anonymous object responses.                                                 | **Medium** | Extract orphan logic to `IAdminService` / `AdminService`                                          |
| ResetEndpoints           | Reset logic injects DbContext directly                                                   | State transition logic (Finished→LastDay, etc.) belongs in a service.                                                   | **Medium** | Extract to `IResetService` / `ResetService`                                                       |
| DbContext                | OnModelCreating (~90 lines) in a single method                                           | Should use `IEntityTypeConfiguration<T>` for consistency with standard.                                                 | **Low**    | Split into configuration classes                                                                  |
| Seeding duplication      | Default warband/motive seeding exists in both Program.cs and AuthService                 | Bug fixes must be applied in two places.                                                                                | **Medium** | Consolidate into AuthService (single source of truth)                                             |
| MotiveFlags enum         | `MotiveFlags` enum in `WarcraftEnums.cs` is unused                                       | Dead code. Motives are now a many-to-many relationship.                                                                 | **Low**    | Remove unused enum                                                                                |
| Tests                    | No tests exist                                                                           | Cannot verify behavior after refactoring, no safety net.                                                                | **Medium** | Add service unit tests progressively                                                              |
| UpdateCharacterRequest   | `OwnerUserId` field allows changing character ownership via the update endpoint          | Potential privilege escalation — any authenticated user could reassign a character to another user.                     | **High**   | Remove `OwnerUserId` from `UpdateCharacterRequest`. Ownership changes should be admin-only.       |
| GetByIdAsync endpoints   | `GetCharacterById` and `GetContentById` don't verify `OwnerUserId`                       | Any authenticated user can view any character/content by guessing the Guid.                                             | **Medium** | Add ownership check or accept as intended (shared data).                                          |
| DTOs folder              | DTOs use `*DTOs.cs` naming instead of `*Requests.cs` / `*Responses.cs` separation        | Minor structural inconsistency with the standard.                                                                       | **Low**    | Reorganize into `Contracts/Requests/` and `Contracts/Responses/`                                  |
| CsvImportHelper          | ~300 lines mixing CSV parsing, domain import logic, and enum conversion                  | Too many responsibilities in a single helper.                                                                           | **Low**    | Split into `CsvParser` (generic) and move import logic to `DataService`                           |
| AccessTokenMinutes       | Default is 525600 (1 year) — effectively no expiration                                   | JWT tokens are long-lived. Refresh tokens exist but the access token itself never expires in practice.                  | **Low**    | Reduce to 15-60 minutes once refresh flow is fully tested                                         |

---

## 7. Code Style and Comment Cleanup

### Overall Assessment

The codebase reads naturally and does **not** look AI-generated. Comment usage is minimal and mostly limited to section dividers (`// ── Login ────`, `// ── Helpers ────`). Code structure is clean with consistent formatting.

### Comments to Remove

The section divider comments (e.g., `// ── Login ────`) in Program.cs are useful given its current size. Once Program.cs is reduced, the dividers become unnecessary. In services, these dividers are already minimal and contextually appropriate.

### Comments to Keep

- XML documentation on `DifficultyFlags`, `Frequency`, `TrackingStatus` enums — these clarify bitmask semantics.
- `/// <summary>` on `Content.AllowedDifficulties` and `Tracking.Difficulty` — clarifies the dual-use of `DifficultyFlags`.
- `/// <summary>` on `Character.OwnerUserId` — explains nullable design for multi-user scenarios.
- `/// <summary>` on `SeedSettings` properties — documents the expected CSV file patterns.

### Naming Observations

- **Good:** Entity names are clear (`Character`, `Content`, `Tracking`, `Warband`, `UserMotive`).
- **Good:** Service names follow consistent patterns (`CharacterService`, `ContentService`, `TrackingService`).
- **Good:** Endpoint grouping with `MapGroup` and `WithTags` is consistent across all files.
- **Good:** DTOs use C# records throughout.
- **Improvable:** `HttpContextHelper.cs` should be `HttpContextExtensions.cs` since it contains extension methods.
- **Improvable:** `Data/` folder should be `Infrastructure/Persistence/` to match the standard.

### Code That Could Be More Compact

- `AppDbContext.UpdateTimestamps()` uses separate switch statements for Added and Modified. Could use a pattern with a common interface or base class, but the current approach is explicit and clear — acceptable as-is.
- `DataEndpoints` contains `ValidClasses` and `ValidRaces` HashSets that duplicate `wowConstants.ts` from the frontend. These should be shared or at least documented as mirrors.

### Files That Would Benefit from Splitting

- `CsvImportHelper.cs` (~300 lines) — Split CSV parsing from domain import logic.
- `DataEndpoints.cs` (~330 lines) — Extract to service.
- `AdminEndpoints.cs` (~180 lines) — Extract orphan management to service.
- `AuthDTOs.cs` — Contains `ClaimOrphanRequest` which belongs to admin, not auth.

---

## 8. Proposed Target Architecture for This Project

```
WarcraftArchive.Api/
  Endpoints/
    AdminEndpoints.cs
    AuthEndpoints.cs
    CharacterEndpoints.cs
    ContentEndpoints.cs
    DashboardEndpoints.cs
    DataEndpoints.cs
    ResetEndpoints.cs
    TrackingEndpoints.cs
    UserMotiveEndpoints.cs
    WarbandEndpoints.cs
  Contracts/
    Requests/
      CreateCharacterRequest.cs
      UpdateCharacterRequest.cs
      CreateContentRequest.cs
      UpdateContentRequest.cs
      CreateTrackingRequest.cs
      UpdateTrackingRequest.cs
      CreateWarbandRequest.cs
      UpdateWarbandRequest.cs
      CreateUserMotiveRequest.cs
      UpdateUserMotiveRequest.cs
      CreateUserRequest.cs
      UpdateUserRequest.cs
      LoginRequest.cs
      RefreshRequest.cs
      LogoutRequest.cs
      ClaimOrphanRequest.cs
      ReorderWarbandItem.cs
    Responses/
      CharacterResponse.cs
      ContentResponse.cs
      TrackingResponse.cs
      WarbandResponse.cs
      UserMotiveResponse.cs
      UserResponse.cs
      LoginResponse.cs
      RefreshResponse.cs
      MeResponse.cs
      WeeklyDashboardResponse.cs
  Domain/
    Entities/
      Auth/
        User.cs
        RefreshToken.cs
        UserMotive.cs
        Warband.cs
      Warcraft/
        Character.cs
        Content.cs
        Tracking.cs
    Enums/
      DifficultyFlags.cs
      Frequency.cs
      TrackingStatus.cs
  Application/
    Interfaces/
      IAuthService.cs
      ICharacterService.cs
      IContentService.cs
      ITrackingService.cs
      IDashboardService.cs
      IWarbandService.cs
      IUserMotiveService.cs
      IAdminService.cs
      IDataService.cs
      IResetService.cs
    Services/
      AuthService.cs
      CharacterService.cs
      ContentService.cs
      TrackingService.cs
      DashboardService.cs
      WarbandService.cs
      UserMotiveService.cs
      AdminService.cs
      DataService.cs
      ResetService.cs
  Infrastructure/
    Persistence/
      AppDbContext.cs
      DatabaseStartupHelper.cs
      Configurations/
        UserConfiguration.cs
        RefreshTokenConfiguration.cs
        WarbandConfiguration.cs
        UserMotiveConfiguration.cs
        CharacterConfiguration.cs
        ContentConfiguration.cs
        TrackingConfiguration.cs
      Migrations/
        (existing migration files)
  Configuration/
    CorsSettings.cs
    DatabaseSettings.cs
    JwtSettings.cs
    SeedSettings.cs
    ServiceCollectionExtensions.cs
  Common/
    HttpContextExtensions.cs
    CsvParser.cs
  Middleware/
    ErrorHandlingMiddleware.cs
  Program.cs                (< 50 lines)
  appsettings.json
  appsettings.Development.json
```

---

## 9. Refactor Phases

### Phase 0 — Baseline Safety

**Goal:** Ensure the application works correctly before any changes.

**Actions:**

- [x] Verify the app builds and runs locally
- [x] Test key endpoints manually (login, list characters, create tracking, dashboard)
- [x] Back up the SQLite database
- [x] Commit current state with a clear message ("pre-refactor baseline")

**Risks:** None.
**Verification:** App starts, Swagger loads, login works, data displays.
**Frontend impact:** None.

---

### Phase 1 — Extract Program.cs Startup Logic

**Goal:** Move migration consolidation, seeding, CSV import orchestration, and helper functions out of `Program.cs`.

**Files affected:**

- `Program.cs` (reduce from ~300 to ~50 lines)
- New: `Infrastructure/Persistence/DatabaseStartupHelper.cs`
- New: `Configuration/ServiceCollectionExtensions.cs`

**Actions:**

- [x] Create `DatabaseStartupHelper` class with `ApplyMigrationsAsync` and legacy migration patching
- [x] Move `SeedAdminAsync` and `SeedDefaultUserDataAsync` to `DatabaseStartupHelper` (called at startup) and consolidate with `AuthService.SeedDefaultUserDataAsync` (called on user creation)
- [x] Create `ServiceCollectionExtensions` with extension methods for EF Core, JWT, CORS, Swagger, and service registration
- [x] Move `ApplyEnvOverride`, `ApplyEnvOverrideInt`, `ApplyEnvOverrideBool` to a static `EnvironmentHelper` class or inline in extensions
- [x] Reduce `Program.cs` to orchestration-only code
- [x] Verify startup behavior is identical

**Risks:** Low — pure code movement, no logic changes.
**Verification:** App starts, migrations apply, admin seeding works, CSV import triggers correctly.
**Frontend impact:** None.

---

### Phase 2 — Extract Entity Configurations

**Goal:** Split `OnModelCreating` into individual `IEntityTypeConfiguration<T>` classes.

**Files affected:**

- `Data/AppDbContext.cs` (reduce `OnModelCreating` to `ApplyConfigurationsFromAssembly`)
- New: `Infrastructure/Persistence/Configurations/` (one file per entity)

**Actions:**

- [x] Create configuration class for each of the 7 entities: `UserConfiguration`, `RefreshTokenConfiguration`, `WarbandConfiguration`, `UserMotiveConfiguration`, `CharacterConfiguration`, `ContentConfiguration`, `TrackingConfiguration`
- [x] Replace `OnModelCreating` body with `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`
- [x] Move DbContext from `Data/` to `Infrastructure/Persistence/`
- [x] Verify migrations still work (no model changes)

**Risks:** Low — must ensure exact same configuration is produced. Run `dotnet ef migrations has-pending-model-changes` to verify.
**Verification:** No pending model changes detected. App starts normally.
**Frontend impact:** None.

---

### Phase 3 — Extract Data Service

**Goal:** Move the ~330 lines of CSV export/import logic from `DataEndpoints.cs` to a proper service.

**Files affected:**

- `DataEndpoints.cs` (reduce from ~330 to ~80 lines)
- New: `Application/Interfaces/IDataService.cs`
- New: `Application/Services/DataService.cs`
- New: `Common/CsvParser.cs` (extract generic CSV parsing from `CsvImportHelper`)

**Actions:**

- [x] Create `IDataService` with methods: `ExportCharactersAsync`, `ExportContentAsync`, `ExportProgressAsync`, `ImportCharactersAsync`, `ImportContentAsync`, `ImportProgressAsync`
- [x] Move CSV generation, validation (class/race lists), and import logic to `DataService`
- [x] Extract generic CSV parsing (`ParseCsvText`, `SplitCsvLines`, `SplitCsvRow`) to `Common/CsvParser.cs`
- [x] Keep domain-specific parsing (difficulty flags, frequency, status) in service or mapping helpers
- [x] Simplify endpoint to delegation + HTTP response mapping
- [x] Register `IDataService` in DI

**Risks:** Low-Medium — import logic is complex but self-contained.
**Verification:** Test export/import round-trip with CSV files. Verify validation errors are reported correctly.
**Frontend impact:** None (API contract unchanged).

---

### Phase 4 — Extract Admin Service

**Goal:** Move orphan management logic from `AdminEndpoints.cs` to a proper service.

**Files affected:**

- `AdminEndpoints.cs` (reduce from ~180 to ~80 lines)
- New: `Application/Interfaces/IAdminService.cs`
- New: `Application/Services/AdminService.cs`

**Actions:**

- [x] Create `IAdminService` with methods: `GetOrphansAsync`, `ClaimOrphanCharacterAsync`, `ClaimOrphanContentAsync`, `DeleteOrphanCharacterAsync`, `DeleteOrphanContentAsync`, `DeleteOrphanTrackingAsync`, `DeleteAllOrphansAsync`
- [x] Move orphan queries and operations from endpoint to service
- [x] Create typed response DTOs for orphan data (replace anonymous objects)
- [x] Simplify endpoint to thin delegation
- [x] Register `IAdminService` in DI

**Risks:** Low — orphan management is straightforward CRUD.
**Verification:** Test orphan listing, claiming, and deletion. Verify admin authorization still enforced.
**Frontend impact:** None (response shape should remain compatible, or update frontend if adding typed DTOs).

---

### Phase 5 — Extract Reset Service

**Goal:** Move daily/weekly reset logic from `ResetEndpoints.cs` to a proper service.

**Files affected:**

- `ResetEndpoints.cs` (reduce from ~80 to ~25 lines)
- New: `Application/Interfaces/IResetService.cs`
- New: `Application/Services/ResetService.cs`

**Actions:**

- [x] Create `IResetService` with methods: `ApplyDailyResetAsync`, `ApplyWeeklyResetAsync`
- [x] Move state transition logic (Finished→LastDay/LastWeek, InProgress/Pending→NotStarted) to service
- [x] Simplify endpoint to thin delegation
- [x] Register `IResetService` in DI

**Risks:** Low — reset logic is self-contained and deterministic.
**Verification:** Test daily reset: Finished→LastDay, LastDay→NotStarted. Test weekly reset includes daily. Verify tracking counts match.
**Frontend impact:** None.

---

### Phase 6 — Reorganize Folder Structure

**Goal:** Move files into the target architecture folders.

**Files affected:**

- `Models/Auth/*.cs` → `Domain/Entities/Auth/*.cs`
- `Models/Warcraft/*.cs` → `Domain/Entities/Warcraft/*.cs` (except enums)
- `Models/Warcraft/WarcraftEnums.cs` → Split into `Domain/Enums/DifficultyFlags.cs`, `Frequency.cs`, `TrackingStatus.cs` (remove unused `MotiveFlags`)
- `DTOs/*.cs` → `Contracts/Requests/` and `Contracts/Responses/`
- `Helpers/HttpContextHelper.cs` → `Common/HttpContextExtensions.cs`
- `Helpers/CsvImportHelper.cs` → Remove (replaced by `CsvParser.cs` + `DataService`)
- `Data/AppDbContext.cs` → `Infrastructure/Persistence/AppDbContext.cs`
- Service interfaces → `Application/Interfaces/`
- Service implementations → `Application/Services/`

**Actions:**

- [x] Move files to new locations
- [x] Update all `using` statements and namespaces
- [x] Remove unused `MotiveFlags` enum
- [x] Remove `CsvImportHelper.cs` (functionality now in `CsvParser` + `DataService`)
- [x] Verify build succeeds
- [x] Update any paths in configuration

**Risks:** Low — namespace changes can cause compile errors. Use IDE refactoring tools.
**Verification:** Clean build with no errors.
**Frontend impact:** None (internal refactor only).

---

### Phase 7 — Reorganize Contracts

**Goal:** Split flat DTO files into clearer request/response separation.

**Files affected:**

- `DTOs/*.cs` files → `Contracts/Requests/` and `Contracts/Responses/`

**Actions:**

- [x] Split `AuthDTOs.cs` into request and response files
- [x] Split `CharacterDTOs.cs` into `CreateCharacterRequest.cs`, `UpdateCharacterRequest.cs`, `CharacterResponse.cs`
- [x] Apply same pattern to Content, Tracking, Warband, UserMotive DTOs
- [x] Move `ClaimOrphanRequest` from Auth DTOs to its own file
- [x] Move `WeeklyDashboardDto` from `TrackingDTOs.cs` to `Responses/WeeklyDashboardResponse.cs`
- [x] Update all imports

**Risks:** Low — pure reorganization.
**Verification:** Clean build. Frontend responses unchanged.
**Frontend impact:** None.

---

### Phase 8 — Security Hardening and Validation

**Goal:** Fix the ownership bypass issues and improve input validation consistency.

**Files affected:**

- `CharacterDTOs.cs` / `Contracts/Requests/UpdateCharacterRequest.cs`
- `CharacterEndpoints.cs`
- `CharacterService.cs`

**Actions:**

- [x] Remove `OwnerUserId` from `UpdateCharacterRequest` — ownership changes should be admin-only
- [x] Add ownership verification to `GetCharacterById` and `GetContentById` (or document that cross-user viewing is intended)
- [x] Review all endpoints for consistent authorization checks
- [x] Reduce `AccessTokenMinutes` from 525600 (1 year) to a reasonable value (15-60 minutes) once refresh flow is confirmed working in production
- [x] Verify all admin endpoints check `IsAdmin()` before processing

**Risks:** Medium — removing `OwnerUserId` from update request may break frontend if it sends this field.
**Verification:** Test character update without OwnerUserId. Verify admin-only operations still work. Test token refresh cycle.
**Frontend impact:** Minor — frontend may need to stop sending `OwnerUserId` on character updates.

---

### Phase 9 — Code Style Cleanup

**Goal:** Remove unnecessary code, rename unclear identifiers, clean up dead code.

**Files affected:** Various files across the project.

**Actions:**

- [x] Remove unused `MotiveFlags` enum (if not done in Phase 6)
- [x] Remove section divider comments from files that are now thin
- [x] Rename `HttpContextHelper.cs` → `HttpContextExtensions.cs`
- [x] Rename `Data/` → `Infrastructure/Persistence/` (if not done in Phase 6)
- [x] Review new service methods for clear naming
- [x] Remove any dead code from refactoring

**Risks:** None.
**Verification:** Code review.
**Frontend impact:** None.

---

### Phase 10 — Tests

**Goal:** Add initial test coverage for the most critical services.

**Files affected:**

- New: `WarcraftArchive.Api.Tests/` project

**Actions:**

- [ ] Create test project with xUnit + EF Core SQLite in-memory
- [ ] Add unit tests for `AuthService` (login, refresh rotation, reuse detection, logout)
- [ ] Add unit tests for `TrackingService` (difficulty validation, duplicate prevention, ownership checks)
- [ ] Add unit tests for `ResetService` (state transitions: Finished→LastDay→NotStarted, Finished→LastWeek→NotStarted)
- [ ] Add unit tests for `DataService` (CSV parsing, validation, import idempotency)
- [ ] Add integration tests for auth flow (login → refresh → logout)

**Risks:** None — additive.
**Verification:** All tests pass.
**Frontend impact:** None.

---

## 10. Detailed Implementation Checklist

### Phase 0 — Baseline

- [x] Verify app builds and runs
- [x] Back up SQLite database
- [x] Commit current state

### Phase 1 — Program.cs Extraction

- [x] Create `Infrastructure/Persistence/DatabaseStartupHelper.cs`
- [x] Move `ApplyMigrationsAsync` and legacy migration patching
- [x] Move `SeedAdminAsync` and `SeedDefaultUserDataAsync`
- [x] Consolidate seeding logic with `AuthService.SeedDefaultUserDataAsync`
- [x] Create `Configuration/ServiceCollectionExtensions.cs`
- [x] Move EF Core, JWT, CORS, Swagger, service registration to extensions
- [x] Move env override helpers to appropriate location
- [x] Reduce Program.cs to ~50 lines
- [x] Verify startup behavior is identical

### Phase 2 — Entity Configurations

- [x] Create `Infrastructure/Persistence/Configurations/` folder
- [x] Create `UserConfiguration.cs`
- [x] Create `RefreshTokenConfiguration.cs`
- [x] Create `WarbandConfiguration.cs`
- [x] Create `UserMotiveConfiguration.cs`
- [x] Create `CharacterConfiguration.cs`
- [x] Create `ContentConfiguration.cs`
- [x] Create `TrackingConfiguration.cs`
- [x] Replace OnModelCreating with `ApplyConfigurationsFromAssembly`
- [x] Run `dotnet ef migrations has-pending-model-changes` to verify
- [x] Move DbContext to `Infrastructure/Persistence/`

### Phase 3 — Data Service

- [x] Create `Application/Interfaces/IDataService.cs`
- [x] Create `Application/Services/DataService.cs`
- [x] Move character export logic
- [x] Move content export logic
- [x] Move progress export logic
- [x] Move character import logic (with class/race validation)
- [x] Move content import logic (with difficulty parsing, motive auto-creation)
- [x] Move progress import logic (with character/content lookup, difficulty validation)
- [x] Extract generic CSV parser to `Common/CsvParser.cs`
- [x] Register service in DI
- [x] Test export/import round-trip

### Phase 4 — Admin Service

- [x] Create `Application/Interfaces/IAdminService.cs`
- [x] Create `Application/Services/AdminService.cs`
- [x] Move orphan listing query
- [x] Move orphan claiming (character + content)
- [x] Move orphan deletion (individual + bulk)
- [x] Create typed response DTOs for orphan data
- [x] Register service in DI

### Phase 5 — Reset Service

- [x] Create `Application/Interfaces/IResetService.cs`
- [x] Create `Application/Services/ResetService.cs`
- [x] Move daily reset logic
- [x] Move weekly reset logic (includes daily)
- [x] Register service in DI
- [x] Test state transitions

### Phase 6 — Folder Reorganization

- [x] Move entities to `Domain/Entities/Auth/` and `Domain/Entities/Warcraft/`
- [x] Split enums to `Domain/Enums/` (one file per enum)
- [x] Remove unused `MotiveFlags` enum
- [x] Move helpers to `Common/`
- [x] Move DbContext to `Infrastructure/Persistence/`
- [x] Move service interfaces to `Application/Interfaces/`
- [x] Move service implementations to `Application/Services/`
- [x] Update all namespaces
- [x] Verify clean build

### Phase 7 — Contract Reorganization

- [x] Split each `*DTOs.cs` into request and response files
- [x] Move to `Contracts/Requests/` and `Contracts/Responses/`
- [x] Move `ClaimOrphanRequest` out of Auth DTOs
- [x] Update all imports
- [x] Verify clean build

### Phase 8 — Security Hardening

- [x] Remove `OwnerUserId` from `UpdateCharacterRequest`
- [x] Add ownership checks to `GetByIdAsync` endpoints (or document decision)
- [x] Review admin authorization on all `/admin/*` endpoints
- [x] Reduce `AccessTokenMinutes` to reasonable value

### Phase 9 — Code Style

- [x] Remove unused `MotiveFlags` enum
- [x] Remove unnecessary section dividers
- [x] Rename files as needed
- [x] Remove dead code
- [x] Final code review

### Phase 10 — Tests

- [ ] Create test project
- [ ] Add AuthService tests (login, refresh, reuse detection)
- [ ] Add TrackingService tests (difficulty validation, duplicates)
- [ ] Add ResetService tests (state transitions)
- [ ] Add DataService tests (CSV parsing, import)
- [ ] Add auth integration tests

---

## 11. Migration Strategy

### Current State

Two EF Core migrations exist and are applied correctly. The startup code includes a legacy migration consolidation system that handles upgrades from older multi-migration databases.

### How to Add a Migration

```bash
cd WarcraftArchive/WarcraftArchive.Api
dotnet ef migrations add <MigrationName>
```

### Before Applying

1. Back up `warcraftarchive.db`
2. Review the generated migration file — check for destructive operations
3. For SQLite specifically: check if the migration requires table rebuilds (column renames, type changes, dropping columns)
4. If a table rebuild is needed, verify data is preserved in the Up/Down methods

### SQLite Limitations

- `ALTER TABLE` cannot rename or drop columns in older SQLite versions (EF Core handles this with table rebuilds)
- Table rebuilds temporarily remove foreign key constraints — verify data integrity after
- `ALTER TABLE ADD COLUMN` works but cannot add columns with `NOT NULL` without a default value
- No concurrent write support — avoid running migrations while the app is serving requests

### After Applying

```bash
dotnet ef database update
```

Then verify:

1. App starts without errors
2. Existing data is preserved
3. New columns/tables are present
4. Foreign key relationships work

### Legacy Migration Consolidation

The `ApplyMigrationsAsync` helper in Program.cs (to be moved to `DatabaseStartupHelper`) handles a specific upgrade scenario:

- If the database has any of the 4 legacy migration IDs (`20260223170050_InitialCreate` through `20260225120000_AddOwnerUserIdToContent`), it:
  1. Patches missing schema changes (e.g., `OwnerUserId` column on `Contents`)
  2. Rewrites `__EFMigrationsHistory` to point to the consolidated `20260226000000_InitialCreate` migration
- This is a one-time upgrade path for existing deployments and can be removed once all deployed instances are on the consolidated migration

### Backup Recommendation

Always back up `warcraftarchive.db` before applying migrations, especially when:

- Dropping or renaming columns/tables
- Changing data types
- Modifying relationships or delete behaviors

---

## 12. Frontend Compatibility Notes

### Frontend Architecture

The React frontend at `WarcraftArchive.Front/` consumes the API through:

- **Custom fetch wrapper:** `src/utils/customFetch.ts` — handles JWT injection, automatic 401 token refresh, abort controller management, auto content-type detection
- **API routes:** `src/environments/apiRoutes.ts` — centralized endpoint definitions with static paths and dynamic `byId` functions
- **Service layer:** `src/services/` — 10 service modules with typed async functions
- **Type definitions:** `src/models/api/` — 7 files with TypeScript interfaces mirroring backend DTOs
- **Environment config:** `src/environments/` — dev/prod base URL selection with runtime Docker injection support
- **State management:** Redux Toolkit + redux-persist for auth token storage

### Key API Contracts to Preserve

The frontend relies on these response shapes. Do not change them without updating the corresponding TypeScript types:

| Backend DTO          | Frontend Type     | Used By                                     |
| -------------------- | ----------------- | ------------------------------------------- |
| `LoginResponse`      | `LoginResponse`   | Auth flow (login → store tokens → redirect) |
| `RefreshResponse`    | `RefreshResponse` | Token rotation in `customFetch`             |
| `MeResponse`         | `MeResponse`      | Current user display                        |
| `CharacterDto`       | `Character`       | Character list, warband view                |
| `ContentDto`         | `Content`         | Content management, expansion grouping      |
| `TrackingDto`        | `Tracking`        | Tracking grid, dashboard                    |
| `WeeklyDashboardDto` | `WeeklyDashboard` | Home screen dashboard                       |
| `WarbandDto`         | `Warband`         | Warband management, character grouping      |
| `UserMotiveDto`      | `UserMotive`      | Motive management, content tagging          |
| `UserDto`            | `User`            | Admin user management                       |

### Breaking Changes to Avoid

- Do not rename endpoint routes — the frontend has hardcoded route strings in `apiRoutes.ts`
- Do not change JSON property casing — the frontend expects camelCase (ASP.NET Core default)
- Do not remove fields from response DTOs — the frontend may render `undefined` instead of gracefully handling it
- Do not change the authentication flow — the frontend stores JWT in Redux and uses refresh tokens for rotation
- Do not change the shape of `WeeklyDashboardDto` — it powers the home screen

### Naming Convention

- **Backend:** C# PascalCase properties → serialized as camelCase by default
- **Frontend:** TypeScript camelCase interfaces match the serialized output

### Safe Approach for API Changes

1. Add new fields to existing DTOs (additive, non-breaking)
2. Use `JsonIgnoreCondition.WhenWritingNull` for optional new fields
3. If a field must be removed, deprecate it first (return null) and update the frontend
4. If a route must change, add the new route alongside the old one, update the frontend, then remove the old route

---

## 13. Testing Strategy

### Priority Tests (implement first)

1. **AuthService** — Token security is critical. Test:
   - Login: valid credentials → tokens returned
   - Login: invalid password → null result
   - Login: inactive user → null result
   - Refresh: valid token → new access + refresh tokens, old revoked
   - Refresh: revoked token reuse → all user tokens revoked (theft detection)
   - Logout: token revocation
   - LogoutAll: all active tokens revoked

2. **TrackingService** — Core business logic with complex validation. Test:
   - Create: valid tracking entry
   - Create: duplicate character+content+difficulty → rejected
   - Create: difficulty not in AllowedDifficulties → rejected
   - Create: character not owned by user → rejected
   - Update: difficulty change with duplicate check
   - Delete: ownership verification

3. **ResetService** — State machine logic. Test:
   - Daily reset: Finished→LastDay, LastDay→NotStarted, InProgress→NotStarted, Pending→NotStarted
   - Weekly reset: Finished→LastWeek, LastWeek→NotStarted, InProgress→NotStarted, Pending→NotStarted
   - Weekly reset includes daily reset
   - Non-matching frequencies unaffected

4. **DataService** — CSV import is error-prone. Test:
   - Valid CSV parsing
   - Invalid CSV handling (missing fields, invalid class/race)
   - Duplicate detection (idempotent import)
   - Auto-creation of warbands and motives during import
   - Round-trip: export → import produces same data

### Where Tests Should Live

```
WarcraftArchive.Api.Tests/
  Services/
    AuthServiceTests.cs
    TrackingServiceTests.cs
    ResetServiceTests.cs
    DataServiceTests.cs
  Integration/
    AuthFlowTests.cs
```

### How to Test EF Core + SQLite

Use SQLite in-memory for fast unit tests:

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=:memory:")
    .Options;

using var context = new AppDbContext(options);
context.Database.OpenConnection();
context.Database.EnsureCreated();
```

For tests that need realistic behavior:

```csharp
var dbPath = Path.GetTempFileName();
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;
```

---

## 14. Summary Assessment

| Aspect               | Rating      | Notes                                                                                                 |
| -------------------- | ----------- | ----------------------------------------------------------------------------------------------------- |
| Overall architecture | **Good**    | Service layer with interfaces, organized domains, Minimal API pattern                                 |
| Code organization    | **Good**    | Clear folder structure, models split by subdomain                                                     |
| Service coverage     | **Partial** | 7/10 endpoint groups have services. Admin, Data, Reset bypass service layer.                          |
| Security             | **Good**    | JWT validation at startup, CORS production check, BCrypt, refresh token rotation with reuse detection |
| Error handling       | **Good**    | Global middleware with typed exception mapping                                                        |
| Configuration        | **Good**    | Options pattern, .env loading, environment variable overrides                                         |
| Database             | **Good**    | EF Core with explicit configuration, migration consolidation for legacy upgrades                      |
| DTOs                 | **Good**    | Records throughout, grouped by domain                                                                 |
| Tests                | **Missing** | No test coverage                                                                                      |
| Frontend integration | **Good**    | Clean service layer, typed API routes, custom fetch with auto-refresh                                 |
| Docker readiness     | **Good**    | Dockerfile, env var configuration, path resolution                                                    |

**Bottom line:** WarcraftArchive is the most architecturally mature project in the portfolio. It already follows most best practices — the refactoring work is about closing the remaining gaps (3 endpoint files bypassing services, Program.cs extraction, entity configuration split) rather than fundamental restructuring. The total effort is smaller than other projects.
