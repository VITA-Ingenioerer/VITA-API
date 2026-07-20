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

`Vita.Planning.Api/appsettings.json` is checked into source control and holds every
**non-secret** setting (SharePoint site/drive IDs, e-conomic base URL, CORS origins, capacity
defaults, AzureAd/Graph tenant & client IDs, etc.) — this is deliberate: none of those values
grant access on their own without an accompanying secret, so the app has a working baseline
config in any environment straight out of the repo.

A small set of real secrets are **not** in that file and must be supplied separately (User
Secrets locally, Azure App Service configuration when deployed — see below):

| Key | What it is |
|---|---|
| `ConnectionStrings:PlanningDatabase` | SQL Server connection string (uses Azure AD auth, but still environment-specific) |
| `Economic:AppSecretToken` | e-conomic API app secret |
| `Economic:AgreementGrantToken` | e-conomic API agreement grant token |
| `Virk:Password` | Virk/CVR distribution service password |
| `MicrosoftGraph:ClientSecret` | Graph app registration client secret (SharePoint/Teams/Outlook provisioning) |

There is no `appsettings.Development.json` anymore — it used to hold secrets *and* settings
together and was removed from the repo for that reason. `.gitignore` now blocks
`appsettings.Development.json` / `appsettings.*.local.json` from ever being committed again,
in case you (or Visual Studio) recreate one locally.

## Running & debugging locally (Visual Studio)

The project already has a `UserSecretsId` wired into `Vita.Planning.Api.csproj`, so secrets
live outside the repo in your user profile, not in a project file.

1. In **Solution Explorer**, right-click `Vita.Planning.Api` → **Manage User Secrets**. This
   opens `secrets.json` (physically at
   `%APPDATA%\Microsoft\UserSecrets\b7e6a5b2-9c0e-4a7f-9f2a-1e6d6d6a2f3e\secrets.json` on
   Windows — outside the repo, never committed).
2. Paste in the five secret keys from the table above, e.g.:

   ```json
   {
     "ConnectionStrings": {
       "PlanningDatabase": "Server=tcp:vita-bigben-dev.database.windows.net,1433;Database=vita-bigben-dev;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
     },
     "Economic": {
       "AppSecretToken": "...",
       "AgreementGrantToken": "..."
     },
     "Virk": {
       "Password": "..."
     },
     "MicrosoftGraph": {
       "ClientSecret": "..."
     }
   }
   ```

   (Ask a teammate for the real values, or pull them from the Azure Key Vault / App Service
   used by `vita-planning-api-dev` — see below.) The `Authentication=Active Directory Default`
   connection string authenticates as *your* signed-in identity, so you'll also need `az login`
   or Visual Studio signed in with an account that has access to the `vita-bigben-dev` database.
3. Press **F5** (or `Ctrl+F5`). `launchSettings.json` already sets
   `ASPNETCORE_ENVIRONMENT=Development`, so `appsettings.json` loads, User Secrets overlay on
   top of it, and Swagger UI comes up automatically at `/swagger`.

Equivalent from the CLI:

```bash
dotnet user-secrets set "ConnectionStrings:PlanningDatabase" "..." --project Vita.Planning.Api
dotnet user-secrets set "Economic:AppSecretToken" "..." --project Vita.Planning.Api
dotnet user-secrets set "Economic:AgreementGrantToken" "..." --project Vita.Planning.Api
dotnet user-secrets set "Virk:Password" "..." --project Vita.Planning.Api
dotnet user-secrets set "MicrosoftGraph:ClientSecret" "..." --project Vita.Planning.Api

dotnet build Vita.Planning.slnx
dotnet run --project Vita.Planning.Api
```

A basic health check is exposed at `/health` and `/ping` if you just want to confirm the API
started without opening Swagger.

## Working from VS Code / the terminal instead of Visual Studio

Nothing above is Visual-Studio-specific — User Secrets, `appsettings.json`, and the publish
profile are all plain `dotnet`/MSBuild concepts. This repo also ships `.vscode/launch.json`
and `.vscode/tasks.json` (local-only, gitignored, same as Visual Studio's own hidden project
files) so VS Code's debugger works the same way F5 does in Visual Studio.

**Debugging (F5 equivalent).** Install the C# extension (or C# Dev Kit) in VS Code, open this
folder, set your User Secrets once (same `dotnet user-secrets set ...` commands as above — they
aren't tied to Visual Studio, they live in `%APPDATA%\Microsoft\UserSecrets\...` regardless of
which editor set them), then press **F5**. It runs the `build` task, launches
`Vita.Planning.Api.dll` with `ASPNETCORE_ENVIRONMENT=Development`, and opens the browser at
`https://localhost:60826` once the server reports it's listening. Breakpoints, watch, step-through
all work the same as in Visual Studio.

**Rebuilding from the terminal:**

```bash
dotnet clean Vita.Planning.slnx
dotnet build Vita.Planning.slnx
```

or for auto-rebuild-on-save (VS Code's rough equivalent of Hot Reload):

```bash
dotnet watch run --project Vita.Planning.Api
```

**Publishing to Azure from the terminal.** There's no CI/CD pipeline — deploys are a deliberate,
manual step run from your machine via [`deploy.ps1`](deploy.ps1), which wraps `dotnet publish` +
`az webapp deploy` (zip deploy). Requires `az login` once per session (or `az account set
--subscription 69ba7cf5-6cee-416a-b8e4-b08e1078862e` if you have more than one).

```powershell
./deploy.ps1 -Environment dev   # or -Environment prod
```

The script refuses to run if your working tree has uncommitted changes, or if the current commit
hasn't been pushed to GitHub — deploys should only ever ship a commit that's already on GitHub, so
GitHub stays the source of truth for what's actually live in each environment. Push first, then
deploy.

The `.pubxml` under `PublishProfiles/` (Visual Studio's **Publish** button) still works
mechanically too, and `.vscode/settings.json` points the VS Code Azure App Service extension at
the same target, but `deploy.ps1` is the normal path since it includes the git-state check above.

Same caveat either way: this deploys the app, not its secrets — confirm the App Service's
Application Settings (see below) are already populated before or after your first deploy, or the
deployed app will start and immediately fail.

## Deployment

There are two environments, each its own Azure App Service in resource group `rg-vita-planning`:

| Environment | App Service |
|---|---|
| Dev | `vita-planning-api-dev` |
| Prod | `vita-planning-api-prod` |

There's no branch-to-environment mapping — `deploy.ps1` deploys whatever commit is currently
checked out (and already pushed) to whichever environment you name. The usual flow is: push to
GitHub, deploy to `dev`, verify, then deploy the same commit to `prod`.

**Rollback:** `git checkout` the last-known-good commit and run `deploy.ps1` again — there's no
separate "undo deploy" step, it always ships whatever's currently checked out.

**Reminder:** the script only ships code. The five secret keys below live independently in each
App Service's own configuration and are *not* touched by a deploy — see the next section, and set
them up for `vita-planning-api-prod` too if you haven't yet (it's a separate App Service from
`vita-planning-api-dev`, so it needs its own copy).

## App Service configuration (secrets)

Visual Studio's **Publish** button (via the `vita-planning-api-dev - Web Deploy.pubxml` under
`Vita.Planning.Api/Properties/PublishProfiles/`) still works mechanically too — but whichever way
code reaches an App Service, the deployed package no longer carries secrets with it (that's the
point), so **each** App Service (`vita-planning-api-dev` *and* `vita-planning-api-prod`) needs the
same five secret keys configured independently:

1. Azure Portal → **App Services → *(dev or prod)* → Settings → Environment variables**
   (Configuration blade).
2. Add each secret as an **application setting**, using `__` (double underscore) in place of
   `:` for nested keys — that's the ASP.NET Core convention for environment-variable-style
   configuration:

   | Name | Value |
   |---|---|
   | `ConnectionStrings__PlanningDatabase` | connection string for that environment's database |
   | `Economic__AppSecretToken` | e-conomic app secret |
   | `Economic__AgreementGrantToken` | e-conomic agreement grant token |
   | `Virk__Password` | Virk service password |
   | `MicrosoftGraph__ClientSecret` | Graph client secret |

   If an App Service used to work before the cleanup, it's likely these were never set
   independently — the app was relying on the (now-deleted) `appsettings.Development.json`
   being bundled into every publish, which also meant secrets were being redeployed to Azure
   on every publish. Worth checking this blade before your next deploy; if these are missing,
   the app will deploy successfully but crash on startup (500s / "Application Error").
3. Also confirm **App Service → Configuration → General settings** does *not* have
   `ASPNETCORE_ENVIRONMENT=Development` set — with `appsettings.json` now covering the
   non-secret baseline, the app should run in `Production` (the App Service default) so
   Swagger UI stays off and the stricter `PlannerAccess`/fallback auth policy applies.
4. For anything beyond a small dev instance, prefer **Key Vault references**
   (`@Microsoft.KeyVault(SecretUri=...)` as the app setting value) over raw values in App
   Service configuration — same `__` naming, but the secret itself lives in Key Vault with its
   own access policy and rotation story.

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
