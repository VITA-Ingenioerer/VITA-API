# Vita.Planning

Internal resource-planning and project-lifecycle API for Vita Ingeniørfirma. An ASP.NET Core
(.NET 10) Web API that sits between **e-conomic** (ERP/time registration), **Microsoft 365**
(Entra ID, Graph, SharePoint, Teams, Outlook), and Vita's resource-planning frontend.

## What it does

- **Resource planning** — capacity profiles/overrides, resource plan entries and scenarios,
  snapshots, Danish public holidays and Vita-specific holiday overrides.
- **Projects & offers lifecycle** — create projects, convert offers to projects, and
  auto-provision the accompanying Microsoft 365 workspace (M365 Group → SharePoint site,
  Teams team, Outlook mail folder, offer-folder/workbook copy on conversion). See
  [docs/project-lifecycle-flows.md](docs/project-lifecycle-flows.md).
- **Time registration** — a thin API in front of e-conomic time entries
  (create/update/approve/delete).
- **Sync** — keeps local mirror tables in step with e-conomic and Entra ID/Graph (projects,
  customers, project groups/statuses, activities, employees, users), scheduled and
  on-demand via `/api/sync/*`.
- **Observability** — an audit trail so an engineer can answer "what happened, who did it,
  and did it work": business events (who created/changed what), resource-plan-entry history,
  a per-step project-workspace-provisioning log, sync-run history, and a durable log of
  unhandled exceptions — all correlated by a per-request correlation ID.

## Architecture

| Project | Responsibility |
|---|---|
| `Vita.Planning.Api` | ASP.NET Core Web API — controllers, auth, middleware, DI wiring (`Program.cs`) |
| `Vita.Planning.Application` | DTOs and service/client interfaces — no dependency on Infrastructure or Api |
| `Vita.Planning.Infrastructure` | EF Core `DbContext` + entities, service implementations, external clients (e-conomic, Microsoft Graph, SharePoint, Virk, DAWA) |
| `Vita.Planning.Domain` | Core domain types |

Solution file: [`Vita.Planning.slnx`](Vita.Planning.slnx).

The frontend (SharePoint Framework web parts for resource planning and time entry) lives in
the sibling `vita-ressourceplan` repository, not here.

## Requirements

- .NET 10 SDK
- SQL Server reachable via the `PlanningDatabase` connection string
- An Entra ID (Azure AD) app registration for bearer-token auth, exposing a `Planner.Access`
  scope (general API access) and a `Planner.Admin` scope (observability/history endpoints)
- e-conomic API credentials and a Microsoft Graph app registration (client ID/secret) for
  SharePoint/Teams/Outlook workspace provisioning

## Configuration

Settings live in `Vita.Planning.Api/appsettings.Development.json` (fill in your own
credentials locally, or use `dotnet user-secrets` / environment variables instead of
committing real secrets). Key sections:

- `ConnectionStrings:PlanningDatabase`
- `AzureAd` — bearer auth for this API
- `Economic` — e-conomic API base URL and credentials
- `MicrosoftGraph` — tenant/client ID/secret used for M365 workspace provisioning
- `ProjectWorkspace` — SharePoint folder paths, mailbox naming, default owner
- `Tilbudssager` / `OutlookTilbudssager` / `RessourceplanWorkbook` — offer/SharePoint archive
  integration settings
- `CapacityDefaults` — baseline weekly-hours fallback for capacity generation
- `Virk` — Danish business registry lookup
- `Cors:AllowedOrigins` — origins allowed to call the API (the frontend's URL(s))

## Running locally

```bash
dotnet build Vita.Planning.slnx
dotnet run --project Vita.Planning.Api
```

Swagger UI is available at `/swagger` in the Development environment. A basic health check
is exposed at `/health` and `/ping`.

## Key API areas

| Route prefix | Covers |
|---|---|
| `/api/projects`, `/api/offers` | Project/offer creation, conversion, metadata |
| `/api/resource-plans`, `/api/resource-plan-entries`, `/api/resource-plan-scenarios` | Resource planning |
| `/api/employee-capacity-*`, `/api/vita-holiday-overrides`, `/api/public-holiday-calendars` | Capacity & holidays |
| `/api/time-entries` | Time registration (e-conomic proxy) |
| `/api/sync/*` | e-conomic / Graph sync operations |
| `/api/import/*` | Bulk imports (capacity, planning metadata, legacy resource plan) |
| `/api/history/*` | Observability — business events, resource-plan-entry history, sync runs, unhandled errors (requires `Planner.Admin`) |
| `/api/project-lifecycle-log` | Per-step log of project/offer workspace provisioning |

## Observability

Every request gets a correlation ID (`X-Correlation-Id` request/response header), which
downstream audit writes default to unless a caller explicitly threads a different one
through (e.g. a bulk import spanning multiple records). This lets you pull one ID and see
everything that happened during a single operation across:

- `core.business_events` — who created/changed what, with old/new values
- `core.resource_plan_entry_history` — planned-hours changes
- `core.project_lifecycle_log` — Outlook/SharePoint/Teams provisioning steps, with
  success/failure per step
- `ops.sync_runs` / `ops.sync_errors` — sync and bulk-import run outcomes
- `ops.errors` — unhandled exceptions, caught by global exception-handling middleware

All of it is read-only and gated behind the `Planner.Admin` authorization policy.

## Further docs

- [docs/project-lifecycle-flows.md](docs/project-lifecycle-flows.md) — project creation and
  offer-to-project conversion, request/response shapes, error handling
- [docs/api-new-endpoints.md](docs/api-new-endpoints.md) — reference for newer/changed
  endpoints (project metadata, etc.)
