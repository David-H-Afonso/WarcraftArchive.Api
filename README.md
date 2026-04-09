# WarcraftArchive API

RESTful API for WarcraftArchive — a personal World of Warcraft progress tracker for characters, warbands, content, and weekly routines.

## Features

- **Character Management** — Create and manage your WoW roster with class, race, level, covenant, and warband
- **Warband System** — Group characters into warbands with custom display ordering
- **Content Catalogue** — Track dungeons, raids, and activities per expansion
- **Progress Tracking** — Per-character content tracking with status, frequency, difficulty, and custom motives
- **Dashboard** — Aggregated weekly summary of trackings grouped by status
- **Data Export/Import** — CSV export & import for characters and trackings
- **Admin Panel** — User management for self-hosted multi-user instances
- **JWT Authentication** — Access + refresh token flow with BCrypt password hashing
- **Seed Support** — Optional admin user and demo data seeding on first run

## Tech Stack

- **.NET 9.0** — ASP.NET Core Minimal API
- **Entity Framework Core 9.0** — SQLite provider
- **JWT Authentication** — BCrypt password hashing
- **Swagger/OpenAPI** — via Swashbuckle

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Installation

```bash
cd WarcraftArchive.Api
cp .env.example .env
# Edit .env — set JWT_SECRET_KEY at minimum
dotnet restore
dotnet ef database update
```

## Development

```bash
dotnet run
# API available at http://localhost:5020
# Swagger UI at http://localhost:5020/swagger
```

## Production (Docker)

```bash
docker build -t warcraftarchive-api .
docker run -p 8080:8080 -v warcraftarchive-data:/data warcraftarchive-api
```

See the root `docker-compose.casaos.yml` for CasaOS deployment.

## API Endpoints

### Authentication

| Method | Route            | Description          |
| ------ | ---------------- | -------------------- |
| POST   | `/auth/register` | Register new user    |
| POST   | `/auth/login`    | Login                |
| POST   | `/auth/refresh`  | Refresh JWT token    |
| POST   | `/auth/logout`   | Revoke refresh token |

### Characters

| Method | Route              | Description            |
| ------ | ------------------ | ---------------------- |
| GET    | `/characters`      | List user's characters |
| GET    | `/characters/{id}` | Get character by ID    |
| POST   | `/characters`      | Create a character     |
| PUT    | `/characters/{id}` | Update a character     |
| DELETE | `/characters/{id}` | Delete a character     |

### Warbands

| Method | Route               | Description          |
| ------ | ------------------- | -------------------- |
| GET    | `/warbands`         | List user's warbands |
| GET    | `/warbands/{id}`    | Get warband by ID    |
| POST   | `/warbands`         | Create a warband     |
| PUT    | `/warbands/{id}`    | Update a warband     |
| DELETE | `/warbands/{id}`    | Delete a warband     |
| PUT    | `/warbands/reorder` | Reorder warbands     |

### Content

| Method | Route            | Description                           |
| ------ | ---------------- | ------------------------------------- |
| GET    | `/contents`      | List content (optional `?expansion=`) |
| GET    | `/contents/{id}` | Get content by ID                     |
| POST   | `/contents`      | Create a content entry                |
| PUT    | `/contents/{id}` | Update a content entry                |
| DELETE | `/contents/{id}` | Delete a content entry                |

### Trackings

| Method | Route             | Description                                                                              |
| ------ | ----------------- | ---------------------------------------------------------------------------------------- |
| GET    | `/trackings`      | List trackings (filters: characterId, status, frequency, expansion, motiveId, contentId) |
| GET    | `/trackings/{id}` | Get tracking by ID                                                                       |
| POST   | `/trackings`      | Create a tracking entry                                                                  |
| PUT    | `/trackings/{id}` | Update a tracking entry                                                                  |
| DELETE | `/trackings/{id}` | Delete a tracking entry                                                                  |

### Motives

| Method | Route           | Description         |
| ------ | --------------- | ------------------- |
| GET    | `/motives`      | List user's motives |
| GET    | `/motives/{id}` | Get motive by ID    |
| POST   | `/motives`      | Create a motive     |
| PUT    | `/motives/{id}` | Update a motive     |
| DELETE | `/motives/{id}` | Delete a motive     |

### Dashboard

| Method | Route               | Description                               |
| ------ | ------------------- | ----------------------------------------- |
| GET    | `/dashboard/weekly` | Weekly tracking summary grouped by status |

### Data (Admin)

| Method | Route                           | Description                     |
| ------ | ------------------------------- | ------------------------------- |
| GET    | `/admin/data/export/characters` | Export characters as CSV        |
| GET    | `/admin/data/export/trackings`  | Export trackings as CSV         |
| POST   | `/admin/data/import`            | Import characters/trackings CSV |

### Admin

| Method | Route               | Description    |
| ------ | ------------------- | -------------- |
| GET    | `/admin/users`      | List all users |
| POST   | `/admin/users`      | Create a user  |
| PUT    | `/admin/users/{id}` | Update a user  |
| DELETE | `/admin/users/{id}` | Delete a user  |

## Project Structure

```
WarcraftArchive.Api/
├── Configuration/        # Strongly-typed settings (JWT, CORS, DB, Seed)
├── Data/                 # EF Core DbContext
├── DTOs/                 # Request/Response DTOs
├── Endpoints/            # Minimal API endpoint maps
│   ├── AdminEndpoints.cs
│   ├── AuthEndpoints.cs
│   ├── CharacterEndpoints.cs
│   ├── ContentEndpoints.cs
│   ├── DashboardEndpoints.cs
│   ├── DataEndpoints.cs
│   ├── TrackingEndpoints.cs
│   ├── UserMotiveEndpoints.cs
│   └── WarbandEndpoints.cs
├── Helpers/              # JWT claims helpers, extension methods
├── Middleware/           # Exception handling middleware
├── Migrations/           # EF Core migrations
├── Models/
│   ├── Auth/             # User, RefreshToken entities + settings
│   └── Warcraft/         # Character, Warband, Content, Tracking, Motive entities
├── Services/             # Business logic (ICharacterService, ITrackingService, ...)
└── Program.cs            # App bootstrap, DI, middleware pipeline
```

## Environment Variables

| Variable                   | Description                       | Default                    |
| -------------------------- | --------------------------------- | -------------------------- |
| `DATABASE_PATH`            | SQLite database file path         | `/data/warcraftarchive.db` |
| `JWT_SECRET_KEY`           | JWT signing key (32+ chars)       | _(required)_               |
| `JWT_ISSUER`               | JWT issuer claim                  | `WarcraftArchive.Api`      |
| `JWT_AUDIENCE`             | JWT audience claim                | `WarcraftArchive.Client`   |
| `JWT_ACCESS_TOKEN_MINUTES` | Access token lifetime (minutes)   | `15`                       |
| `JWT_REFRESH_TOKEN_DAYS`   | Refresh token lifetime (days)     | `30`                       |
| `CORS_ALLOWED_ORIGINS`     | Comma-separated allowed origins   | _(empty)_                  |
| `SEED_ADMIN_ENABLED`       | Create admin user on first run    | `false`                    |
| `SEED_ADMIN_EMAIL`         | Admin user email                  | `admin@local`              |
| `SEED_ADMIN_USERNAME`      | Admin username                    | `admin`                    |
| `SEED_ADMIN_PASSWORD`      | Admin password                    | _(set in .env)_            |
| `DEMO_IMPORT_ENABLED`      | Import demo CSV data on first run | `false`                    |
| `CSV_DATA_PATH`            | Path to demo CSV files            | `/data/csv`                |

## License

MIT
