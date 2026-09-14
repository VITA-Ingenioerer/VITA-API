# Employee classification (faglighed & profession)

Ressourceplan can read and edit three employee attributes. **Microsoft Entra is the
authority for all three** — SQL is a read model and `extensionAttribute1-3` are a derived
mirror. Nothing writes back to Entra except an explicit, authenticated user action.

| Canonical attribute (authority) | Cardinality | Mirrored to |
|---|---|---|
| `VITA.PrimaryFaglighed` | single | `extensionAttribute1` |
| `VITA.SecondaryFagligheder` | multi | `extensionAttribute2` as `\|A\|B\|C\|` |
| `VITA.Profession` | single | `extensionAttribute3` |

## Why the mirror exists

Custom security attributes **cannot** be used in Entra dynamic membership rules — Microsoft
states this explicitly: *"Are custom security attribute assignments available for rules for
dynamic membership groups? No."*
([docs](https://learn.microsoft.com/en-us/entra/identity/users/users-custom-security-attributes))

`extensionAttribute1-3` can. So the canonical values are copied into them after every write,
purely so rules like the following work:

```text
user.extensionAttribute1 -eq "HVAC"
user.extensionAttribute2 -contains "|IKT og BIM|"
```

The leading and trailing pipes are deliberate: `|El|` cannot accidentally match a longer
value that merely starts with `El`.

**The application must never read `extensionAttribute1-3` as truth.** They are write-only
derived data.

## Data flow

```text
WRITE      Ressourceplan → API → Graph (custom security attributes)
                               → Graph (extensionAttribute mirror)
                               → SQL read model
                               → business_events audit row

RECONCILE  Graph → SQL read model        (scheduled, every 30 min by default)

NEVER      SQL → Graph
```

## What was added

**Database** — `Vita.Planning.Infrastructure/Sql/2026-09-employee-classification.sql`
(idempotent, must be run by hand; this repo has no EF migrations):

- `ext.users.primary_faglighed nvarchar(200) NULL`
- `ext.users.profession nvarchar(100) NULL`
- `ext.user_secondary_fagligheder (employee_id, faglighed)`, PK on both, FK to `ext.users`
- supporting indexes for roster-wide "who has faglighed X" filtering

**API**

| Method | Route | Authorization |
|---|---|---|
| `GET` | `/api/employee-classification/options` | `PlannerAccess` |
| `GET` | `/api/employees/{employeeId}/classification` | `PlannerAccess` |
| `PUT` | `/api/employees/{employeeId}/classification` | `EmployeeClassificationWrite` |
| `POST` | `/api/employee-classification/reconcile` | `EmployeeClassificationWrite` |

The write endpoint takes **only** an employee id. The Entra UPN is resolved server-side from
`ext.users` and never accepted from the caller.

`GET /api/users` and `/api/users/{id}` now also return `primaryFaglighed`,
`secondaryFagligheder` and `profession` from the **read model** (not Graph), so the roster
loads in one call.

**Frontend** — the classification editor lives in the existing employee admin page
(Medarbejdere → Brugerprofiler) with its own save button, so a permission error there cannot
block capacity editing.

## Configuration

`appsettings.json` → `EntraClassification`. **No client secret in source** — supply it via
user-secrets / Key Vault, or leave it unset to use the app service's managed identity.

```jsonc
"EntraClassification": {
  "TenantId": "26115a1a-a357-4a85-910a-1dde4db2a914",
  "ClientId": "f4b7d367-0dc3-4694-910e-63a5b0454985",
  "AttributeSet": "VITA",
  "PrimaryAttributeName": "PrimaryFaglighed",
  "SecondaryAttributeName": "SecondaryFagligheder",
  "ProfessionAttributeName": "Profession",
  "OptionsCacheMinutes": 15,
  "ReconcileIntervalMinutes": 30,
  "WriteExtensionAttributeMirror": true,
  "WriteRole": "Planning.EmployeeClassification.Write",
  "WriteUserPrincipalNames": []
}
```

### Credentials

Classification uses the **same app registration** as the other Graph clients, so
`TenantId`, `ClientId` and `ClientSecret` left unset in the `EntraClassification` section
are inherited from the `MicrosoftGraph` section at startup. With
`MicrosoftGraph:ClientSecret` already in user-secrets, **nothing further is needed locally**.

Setting a value explicitly in `EntraClassification` still wins, so the two can be split onto
separate app registrations later without a code change.

To override:

```bash
dotnet user-secrets set "EntraClassification:ClientSecret" "<secret>" --project Vita.Planning.Api
```

### Who may write

`EmployeeClassificationWrite` grants access if **either** is true:

1. the caller's token carries the app role in `WriteRole`, **or**
2. the caller's UPN is listed in `WriteUserPrincipalNames`.

Both default to unassigned/empty, so **nobody can write until one is configured**. The
allowlist exists so the feature is operable before app roles are rolled out:

```jsonc
"WriteUserPrincipalNames": [ "mkj@vitaing.dk" ]
```

## Required Microsoft Graph permissions

App registration: **`vita-resourceplan-msgraph-projects`** (`f4b7d367-0dc3-4694-910e-63a5b0454985`).

Granted and admin-consented on 2026-09-14 — `User.Read.All` was correctly replaced by
`User.ReadWrite.All`. Current application permissions: `Calendars.ReadWrite`,
`CustomSecAttributeAssignment.ReadWrite.All`, `CustomSecAttributeDefinition.Read.All`,
`Group.ReadWrite.All`, `Mail.ReadWrite`, `Sites.ReadWrite.All`, `User.ReadWrite.All`.

The three that this feature required:

| Permission | Type | GUID | Needed for |
|---|---|---|---|
| `CustomSecAttributeAssignment.ReadWrite.All` | Application | `de89b5e4-5b8f-48eb-8925-29c2b33bd8bd` | read/write the three attributes on users |
| `CustomSecAttributeDefinition.Read.All` | Application | `b185aa14-d8d2-42c1-a685-0f5596613624` | read the allowed-value taxonomy |
| `User.ReadWrite.All` | Application | `741f803b-c850-494e-b5df-cde7c675a1ca` | write the `extensionAttribute1-3` mirror |

`User.ReadWrite.All` supersedes the existing `User.Read.All` — `User.Read.All` cannot PATCH
`onPremisesExtensionAttributes`.

### Granting via portal

1. [Entra admin center](https://entra.microsoft.com) → **Applications → App registrations**
   → `vita-resourceplan-msgraph-projects`
2. **API permissions → Add a permission → Microsoft Graph → Application permissions**
3. Add the three above
4. **Grant admin consent for VITA Ingeniører** — the permissions do nothing until consented

### Granting via CLI

```bash
APP_ID=f4b7d367-0dc3-4694-910e-63a5b0454985
GRAPH=00000003-0000-0000-c000-000000000000

az ad app permission add --id $APP_ID --api $GRAPH \
  --api-permissions de89b5e4-5b8f-48eb-8925-29c2b33bd8bd=Role \
                    b185aa14-d8d2-42c1-a685-0f5596613624=Role \
                    741f803b-c850-494e-b5df-cde7c675a1ca=Role

# Requires Privileged Role Administrator or Global Administrator.
az ad app permission admin-consent --id $APP_ID
```

### Directory roles are NOT required — verified 2026-09-14

Microsoft's documentation states that custom security attributes are walled off from normal
admin roles (*"By default, Global Administrator and other administrator roles do not have
permissions to read, define, or assign custom security attributes"*), which reads as though
the service principal also needs the **Attribute Assignment Administrator** directory role.

That prerequisite applies to the **delegated / portal** path. Tested app-only against this
tenant after consent, with no directory role assigned to the service principal:

| Check | Result |
|---|---|
| Client-credentials token for `f4b7d367-…` | acquired |
| `GET /directory/customSecurityAttributeDefinitions/VITA_Profession/allowedValues` | 200 |
| `GET /users?$select=customSecurityAttributes` | 200, VITA attribute set readable |
| `GET /users/{upn}?$select=onPremisesExtensionAttributes` | 200 |

So the three application permissions plus admin consent are sufficient. If a 403 does appear
later, assigning the service principal **Attribute Assignment Administrator** and **Attribute
Definition Reader** (Entra admin center → Roles and administrators → Add assignment) is the
fallback. The API re-throws Graph 403s naming the three permissions, so the log identifies
the cause.

### Managed identity (preferred in production)

Leave `EntraClassification:ClientSecret` unset and the client authenticates with
`DefaultAzureCredential` instead. The app service's managed identity then needs the same
three Graph app roles assigned to **its** service principal (app-role assignment via Graph;
the portal does not expose this for managed identities). This removes the standing client
secret, which is the main reason to prefer it.

## Deployment order

1. Grant + consent the Graph permissions above.
2. Run `Sql/2026-09-employee-classification.sql` against the target database.
3. Credentials — nothing to do when `MicrosoftGraph:ClientSecret` is already configured
   for that environment; the classification client inherits it.
4. Set `WriteUserPrincipalNames` (or assign the app role) — otherwise every write 403s.
5. Deploy the API, then the SPFx package.
6. Smoke test: `GET /api/employee-classification/options` should return three non-empty lists.

Steps 1 and 2 are independent; the API starts fine without either, and only classification
requests fail.

## Known taxonomy problems (as of 2026-09-14)

Read live from the tenant. These are **data** issues in Entra, not code issues, and the
options endpoint will surface whatever is there.

**`VITA.PrimaryFaglighed` (21 values) contains duplicates and strays:**

- `EL` and `El` — case-variant duplicates
- `Konstruktioner` and `Konstruktioner ` — the second has a trailing space
- `Projektleder` — a stray; `Projektledelse` already exists
- `Afløb` — overlaps `Regnvandshåndtering og afløbsteknik`

**`VITA.SecondaryFagligheder` (19 values) is a different list:**

- **`Brand` is missing** — nobody can hold Brand as a secondary faglighed
- has `VVS`, `IKT`, `BIM`, which Primary does not

Because both attributes are `usePreDefinedValuesOnly: true`, Graph rejects anything off the
list, so the backend validates **each attribute against its own list** rather than a merged
taxonomy. Aligning the two lists is an Entra cleanup task.

Note: Entra has no delete for predefined values — they can only be **deactivated**
(`isActive: false`). The options endpoint already filters deactivated values out, so
deactivating the strays is enough to clean up the dropdowns. Values already assigned to a
user are not removed by deactivating the definition.

**`VITA.Profession` has only two values:** `Ingeniør`, `Teknisk Tegner`. Profession is a
required field on save, so anyone who is neither (installatør, brandrådgiver,
bygningskonstruktør, byggeleder, økonomi/administration) **cannot be saved at all** until
more values are added. Add the missing professions before rollout, or relax the requirement
in `EmployeeIdentityService.Validate`.

## Validation rules

Enforced server-side in `EmployeeIdentityService.Validate`, before any Graph call:

- `PrimaryFaglighed` — required, must exist in the primary allowed list
- `Profession` — required, must exist in the professions allowed list
- `SecondaryFagligheder` — zero or more; each must exist in the **secondary** allowed list;
  duplicates removed; must not contain the primary value

Values are normalized to the exact casing Entra holds, since predefined values match
character-for-character. A failure returns `400` with a Danish message naming the value.

## Auditing

Every successful write records a `business_events` row with `event_type =
EmployeeClassificationUpdated`, holding before/after values, the caller's oid and name, and
`source_module = employee-classification`. These attributes can drive dynamic group
membership and potentially Intune assignment, so the change history matters.

## Failure behaviour

- **Mirror PATCH fails** — logged as an error, request still succeeds. The canonical Entra
  values are already correct; only dynamic-group membership is stale, and the next write or
  reconciliation fixes it. Failing the request would report a successful save as failed.
- **Options fetch fails** — the employee editor degrades to read-only rather than taking the
  whole page down. The roster still loads.
- **Reconciliation fails** — logged; retried on the next interval, not on every sync pass.
