# castlink

A "six degrees of separation" game for movies: given two actors, find the shortest chain of shared
films connecting them (à la Six Degrees of Kevin Bacon). Includes a daily challenge with a live
leaderboard and a trivia mode, all built from a TMDB-sourced actor/film graph.

Architecture, data model, API surface, TMDB constraints, and the phased build plan are in
[`docs/PLAN.md`](docs/PLAN.md). This is a portfolio project — see that document for the reasoning
behind each design decision, not just the decision itself.

**Status:** Phase 0 (Foundation) — solution skeleton, local dev services, and CI are in place. No
game logic yet; that starts in Phase 1.

## Stack

ASP.NET Core Web API (.NET 8) · Blazor WebAssembly · PostgreSQL + EF Core · Redis · SignalR · TMDB API

## Project layout

```
src/
  Castlink.Domain/          # entities, value objects — zero external deps
  Castlink.Application/     # use cases, port interfaces, the BFS algorithm, scoring
  Castlink.Infrastructure/  # EF Core, Redis, TMDB HTTP client
  Castlink.Ingestion/       # worker host for TMDB sync
  Castlink.Api/             # Web API + SignalR hubs; hosts the Blazor client
  Castlink.Client/          # Blazor WebAssembly UI
  Castlink.Shared/          # DTOs shared by Api + Client (no server-only deps)
tests/
  Castlink.Application.Tests/     # BFS, scoring — pure, no I/O
  Castlink.Infrastructure.Tests/  # ingestion idempotency, via Testcontainers
  Castlink.Api.Tests/             # endpoint integration, via WebApplicationFactory
```

## Local development

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Docker.

1. Start Postgres and Redis:
   ```bash
   docker compose up -d
   ```
2. Set your TMDB API key as a user secret — **never** in `appsettings.json`:
   ```bash
   dotnet user-secrets set "Tmdb:ApiKey" "<your-key>" --project src/Castlink.Api
   dotnet user-secrets set "Tmdb:ApiKey" "<your-key>" --project src/Castlink.Ingestion
   ```
3. Build and test:
   ```bash
   dotnet build castlink.sln
   dotnet test castlink.sln
   ```
4. Run the API (serves `/healthz` for now; the Blazor client and real endpoints land in later phases):
   ```bash
   dotnet run --project src/Castlink.Api
   ```

## TMDB attribution

This product uses the TMDB API but is not endorsed or certified by TMDB. Attribution and the TMDB
logo will be added to the UI in Phase 3, per [TMDB's API terms](https://www.themoviedb.org/api-terms-of-use).
