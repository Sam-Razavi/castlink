# castlink

A "six degrees of separation" game for movies: given two actors, find the shortest chain of shared
films connecting them (à la Six Degrees of Kevin Bacon). Includes a daily challenge with a live
leaderboard and a trivia mode, all built from a TMDB-sourced actor/film graph.

Architecture, data model, API surface, TMDB constraints, and the phased build plan are in
[`docs/PLAN.md`](docs/PLAN.md). This is a portfolio project — see that document for the reasoning
behind each design decision, not just the decision itself.

**Status:** Phase 2 (Shortest path) — an in-memory CSR graph loaded from `credits` at API startup,
bidirectional BFS with deterministic tie-breaking, and `POST /api/path`. No Blazor UI yet; that
starts in Phase 3.

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
3. Apply the database schema (needs the `dotnet-ef` tool: `dotnet tool install --global dotnet-ef --version 8.0.11`):
   ```bash
   dotnet ef database update --project src/Castlink.Infrastructure --startup-project src/Castlink.Infrastructure
   ```
4. Build and test:
   ```bash
   dotnet build castlink.sln
   dotnet test castlink.sln
   ```
   The ingestion idempotency tests (`Castlink.Infrastructure.Tests/Persistence`) spin up their own
   throwaway Postgres via [Testcontainers](https://dotnet.testcontainers.org/) — they need Docker
   running but not the `docker compose` instance from step 1.
5. Run the API (serves `/healthz` for now; the Blazor client and real endpoints land in later phases):
   ```bash
   dotnet run --project src/Castlink.Api
   ```

## Running a real TMDB ingestion

`Castlink.Ingestion` is a run-to-completion batch job, not a long-lived service — it does one full
seed or one incremental sync and exits. Needs steps 1–3 above done first, plus a real TMDB API key
(step 2).

- **Incremental sync** (the default — fetches whatever changed since the last run):
  ```bash
  dotnet run --project src/Castlink.Ingestion
  ```
- **Full seed** (first run only — walks `/discover/movie` year by year from 1970 to now,
  `vote_count >= 200`; expect this to take a while, see docs/PLAN.md Phase 1's open items on
  runtime):
  ```bash
  dotnet run --project src/Castlink.Ingestion -- --Ingestion:Mode=FullSeed
  ```
  It's safe to re-run after a crash or Ctrl-C — progress is checkpointed per release year in
  `sync_state`, and every write is an idempotent upsert (see `PostgresIngestionWriter`).

## TMDB attribution

This product uses the TMDB API but is not endorsed or certified by TMDB. Attribution and the TMDB
logo will be added to the UI in Phase 3, per [TMDB's API terms](https://www.themoviedb.org/api-terms-of-use).
