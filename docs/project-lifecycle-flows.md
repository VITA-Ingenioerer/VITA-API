# Project Lifecycle Flows — Frontend Guide

Covers the two endpoints that create projects and provision Microsoft 365 workspaces.
Both are authenticated (Bearer token required). The caller's identity from the JWT is used
to set the M365 Group owner automatically — the frontend does not need to pass it.

---

## 1. Create Project — `POST /api/projects`

Creates a project in e-conomic and provisions an M365 workspace (SharePoint site, Teams team, Outlook mail folder).

### Request

```json
{
  "name": "Strandboulevarden 100",
  "projectGroupNumber": 1,
  "customerId": 42,
  "responsibleEmployeeNumber": 122,
  "subProjectNames": ["Lejeplads", "Sommerhus"],
  "isPrivate": false,
  "createTeam": true,
  "memberUserIds": [],
  "skipWorkspaceProvisioning": false
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | Yes | Project name used in e-conomic and as the M365 Group display name |
| `projectGroupNumber` | int | No (default `1`) | e-conomic project group |
| `customerId` | int | Yes | Internal customer ID — must have an e-conomic customer number |
| `responsibleEmployeeNumber` | int | No | e-conomic employee number for the responsible person |
| `subProjectNames` | string[] | No | Each entry creates a numbered sub-project in e-conomic (`mainNumber + 1`, `+2`, …). Max 99. |
| `isPrivate` | bool | No (default `false`) | `true` = private M365 Group/Teams team |
| `createTeam` | bool | No (default `true`) | Whether to create a Teams team on the group |
| `memberUserIds` | string[] | No | AAD object IDs or UPNs to add as group members |
| `skipWorkspaceProvisioning` | bool | No (default `false`) | `true` = skip all M365 steps, only create in e-conomic |

### Response `200 OK`

```json
{
  "mainProjectNumber": 26000100,
  "projectName": "Strandboulevarden 100",
  "createdAtUtc": "2026-06-23T09:00:00Z",
  "subProjectsCreated": [26000101, 26000102],
  "subProjectFailures": [],
  "hasSubProjectFailures": false,
  "workspace": {
    "groupId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "sharePointSiteUrl": "https://vitaingdk.sharepoint.com/sites/26000100",
    "teamsId": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
    "outlookFolderId": "AAMk...",
    "offerFolderCopied": false,
    "workbookCopied": false,
    "hasWarnings": false,
    "warnings": []
  }
}
```

**Sub-project failures** — if an e-conomic call fails part-way through, remaining sub-projects are not attempted:

```json
"subProjectFailures": [
  {
    "subProjectNumber": 26000102,
    "subProjectName": "Strandboulevarden 100 - Sommerhus",
    "wasAttempted": true,
    "error": "Project already exists in e-conomic."
  }
]
```

**Workspace warnings** — workspace provisioning is best-effort. The project IS created even if workspace fails:

```json
"workspace": {
  "groupId": null,
  "hasWarnings": true,
  "warnings": [
    "Failed to create M365 group: Response status code does not indicate success: 403 (Forbidden)."
  ]
}
```

---

## 2. Convert Offer to Project — `POST /api/offers/{id}/convert-to-project`

Converts an existing offer to a project. Creates the project (+ optional sub-projects) in e-conomic,
provisions an M365 workspace for the main project, and optionally copies the offer's SharePoint
folder and economics workbook into the new project site.

### Request

```json
{
  "projectGroupNumber": 1,
  "responsibleEmployeeNumber": 122,
  "subProjectNames": ["Lejeplads", "Sommerhus"],
  "migrateResourcePlanEntries": true,
  "isPrivate": false,
  "createTeam": true,
  "memberUserIds": [],
  "offerSharePointDriveId": "b!V6ua....",
  "offerSharePointFolderItemId": "01XY....",
  "skipWorkspaceProvisioning": false
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `projectGroupNumber` | int | No (default `1`) | e-conomic project group |
| `responsibleEmployeeNumber` | int | No | e-conomic employee number |
| `subProjectNames` | string[] | No | Sub-projects created in e-conomic only — no separate workspace per sub-project. Max 99. |
| `migrateResourcePlanEntries` | bool | No (default `false`) | Remaps all planned hours from the offer's planning target to the new project's planning target, then deactivates the offer target |
| `isPrivate` | bool | No (default `false`) | `true` = private M365 Group/Teams team |
| `createTeam` | bool | No (default `true`) | Whether to create a Teams team |
| `memberUserIds` | string[] | No | AAD object IDs or UPNs to add as members |
| `offerSharePointDriveId` | string | No | Drive ID of the offer's tilbudssager document library. When provided together with `offerSharePointFolderItemId`, the entire offer folder is copied to `D1 Grundlag/{offerFolderName}` in the new project site, and the economics workbook is copied to `C03 Økonomi/C03.10 VITA Projektøkonomi/`. |
| `offerSharePointFolderItemId` | string | No | Item ID of the offer's SharePoint folder |
| `skipWorkspaceProvisioning` | bool | No (default `false`) | `true` = skip all M365 steps |

> **Where to get `offerSharePointDriveId` and `offerSharePointFolderItemId`?**
> These are returned when the offer's SharePoint folder was originally created (from the tilbudssager flow).
> Store them on the offer in the frontend when the folder is created, then pass them back here on conversion.

### Response `200 OK`

```json
{
  "offerId": 5,
  "offerNumber": "T-2026-042",
  "mainProjectNumber": 26000100,
  "projectName": "Strandboulevarden 100",
  "convertedAtUtc": "2026-06-23T09:00:00Z",
  "subProjectsCreated": [
    { "subProjectNumber": 26000101, "subProjectName": "Strandboulevarden 100 - Lejeplads" },
    { "subProjectNumber": 26000102, "subProjectName": "Strandboulevarden 100 - Sommerhus" }
  ],
  "subProjectFailures": [],
  "hasSubProjectFailures": false,
  "resourcePlanEntriesMigrated": 14,
  "workspace": {
    "groupId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "sharePointSiteUrl": "https://vitaingdk.sharepoint.com/sites/26000100",
    "teamsId": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
    "outlookFolderId": "AAMk...",
    "offerFolderCopied": true,
    "workbookCopied": true,
    "hasWarnings": false,
    "warnings": []
  }
}
```

`resourcePlanEntriesMigrated` is `null` when `migrateResourcePlanEntries` was `false`.

---

## What the M365 workspace creates

For the **main project** (both flows):

| Step | What is created | Location |
|---|---|---|
| M365 Group | Group with `mailNickname = mainProjectNumber` | AAD |
| SharePoint site | Auto-provisioned by the group | `https://vitaingdk.sharepoint.com/sites/{mainProjectNumber}` |
| Teams team | Linked to the group | Public or private per `isPrivate` |
| Outlook folder | Folder named after the project | `{yy}-mail@vitaing.dk` mailbox |
| Offer folder copy *(conversion only)* | Full offer folder → `D1 Grundlag/{offerFolderName}/` | Project SharePoint site |
| Workbook copy *(conversion only)* | `VITA_C03_Honorar og opfølgning.xlsm` → `C03 Økonomi/C03.10 VITA Projektøkonomi/` | Project SharePoint site |

For **sub-projects**: created in e-conomic only. No separate SharePoint site or Teams team.

---

## Error handling

| Scenario | HTTP status | What to do |
|---|---|---|
| Offer not found | `404` | Show not found |
| Offer already converted | `400` with `{ message }` | Show message |
| Customer not in e-conomic | `400` with `{ message }` | Prompt user to create customer in e-conomic and sync |
| e-conomic call fails | `400` with `{ message }` | Project was NOT created. Safe to retry. |
| Workspace provisioning fails | `200` with `workspace.hasWarnings: true` | Project WAS created. Show warnings. User can provision workspace manually later. |

The project and offer stamp are written to the database **before** workspace provisioning starts.
A workspace failure is never fatal to the conversion — check `workspace.hasWarnings` and surface
`workspace.warnings` to the user.

---

## New project visible in planner

After creation, the new project number won't appear in `GET /api/projects` until the next
e-conomic sync runs. The sync runs automatically every 15 minutes, or can be triggered manually:

```
POST /api/sync/projects
```
