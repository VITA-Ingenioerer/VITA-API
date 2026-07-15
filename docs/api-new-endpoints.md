# New & Changed API Endpoints

## Base URL
All routes are relative to your API base, e.g. `https://localhost:5001`

---

## 1. Project Metadata — `/api/project-metadata`

### GET /api/project-metadata
List all metadata records. Optionally filter by target type.

**Query params:**
| Param | Type | Required | Values |
|-------|------|----------|--------|
| targetType | string | No | `Project`, `Offer` |

**Example:**
```
GET /api/project-metadata?targetType=Project
```

---

### GET /api/project-metadata/project/{projectNumber}
Get metadata for a specific project.

**Example:**
```
GET /api/project-metadata/project/12345
```

**Returns:** `200 ProjectMetadataDto` or `404`

---

### GET /api/project-metadata/offer/{offerId}
Get metadata for a specific offer.

**Example:**
```
GET /api/project-metadata/offer/99
```

**Returns:** `200 ProjectMetadataDto` or `404`

---

### PUT /api/project-metadata/project/{projectNumber}
Create or update metadata for a project (upsert).

**Example:**
```
PUT /api/project-metadata/project/12345
Content-Type: application/json
```

**Body:**
```json
{
  "planningCategory": "Engineering",
  "planningStatus": "Active",
  "disciplineOwner": "MJ",
  "defaultDescription": "Main project",
  "colorTag": "#FF0000",
  "planningGroup": "Group A",
  "phase": "Execution",
  "probabilityPercent": 85.0,
  "budgetHours": 1200.0,
  "budgetRevenue": 500000.0,
  "lastPlanningReviewBy": "martin",
  "priority": 1,
  "isBillableForPlanning": true,
  "isAbsence": false,
  "isInternal": false,
  "isProbableCase": false,
  "isVisibleInPlanner": true,
  "dailyPlanningEnabled": true,
  "startDate": "2025-01-01",
  "endDate": "2025-12-31",
  "updatedBy": "martin"
}
```

**Returns:** `200 ProjectMetadataDto`

---

### PUT /api/project-metadata/offer/{offerId}
Create or update metadata for an offer (upsert). Same body as above.

**Example:**
```
PUT /api/project-metadata/offer/99
Content-Type: application/json
```

**Body:** *(same as project upsert above)*

**Returns:** `200 ProjectMetadataDto`

---

## 2. Project Lifecycle Log — `/api/project-lifecycle-log`

### GET /api/project-lifecycle-log
List log entries. All filters optional.

**Query params:**
| Param | Type | Required |
|-------|------|----------|
| targetType | string | No — `Project` or `Offer` |
| projectNumber | int | No |
| offerId | int | No |
| eventType | string | No |

**Examples:**
```
GET /api/project-lifecycle-log?targetType=Project&projectNumber=12345
GET /api/project-lifecycle-log?targetType=Offer&offerId=99
GET /api/project-lifecycle-log?eventType=StatusChanged
```

---

### GET /api/project-lifecycle-log/{id}
Get a single log entry by id.

**Example:**
```
GET /api/project-lifecycle-log/1001
```

**Returns:** `200 ProjectLifecycleLogDto` or `404`

---

### POST /api/project-lifecycle-log
Manually append a lifecycle log entry.

**Rules:**
- `targetType` must be `"Project"` or `"Offer"`
- Supply `projectNumber` when `targetType` is `"Project"`
- Supply `offerId` when `targetType` is `"Offer"`

**Body:**
```json
{
  "targetType": "Project",
  "projectNumber": 12345,
  "offerId": null,
  "eventType": "StatusChanged",
  "eventTitle": "Status updated",
  "eventDescription": "Changed from Active to Closed",
  "oldValue": "Active",
  "newValue": "Closed",
  "snapshotJson": null,
  "createdBy": "martin"
}
```

**Returns:** `201 Created → ProjectLifecycleLogDto`

---

## 3. Planning Metadata JSON Import — `/api/import`

### POST /api/import/planning-metadata-json
Upload a legacy planning metadata JSON file.

**Content-Type:** `multipart/form-data`

| Field | Type | Required |
|-------|------|----------|
| file | .json file | Yes |

**Example (curl):**
```
curl -X POST https://localhost:5001/api/import/planning-metadata-json \
  -F "file=@planning-metadata.json"
```

---

## 4. Resource Plan Entries — `/api/resource-plan-entries` *(changed)*

> **Breaking change:** `resourcePlanId` is now required on all write operations.

### POST /api/resource-plan-entries
Create a single entry.

**Body:**
```json
{
  "planningTargetId": 42,
  "resourcePlanId": 7,
  "planDate": "2025-06-15",
  "hours": 8.0,
  "description": "Design work",
  "isManualOverride": false,
  "createdBy": "martin"
}
```

---

### POST /api/resource-plan-entries/period
Bulk upsert entries spread over a date range.

**Body:** *(array)*
```json
[
  {
    "planningTargetId": 42,
    "resourcePlanId": 7,
    "fromDate": "2025-06-01",
    "toDate": "2025-06-30",
    "hours": 160.0,
    "description": "Sprint 5",
    "isManualOverride": false,
    "changedBy": "martin"
  }
]
```

---

### POST /api/resource-plan-entries/auto-distribute
Auto-distribute hours across working days (respects DK Vita holidays).

**Body:** *(array)*
```json
[
  {
    "planningTargetId": 42,
    "resourcePlanId": 7,
    "fromDate": "2025-06-01",
    "toDate": "2025-06-30",
    "hours": 160.0,
    "description": "Auto distributed",
    "isManualOverride": false,
    "changedBy": "martin"
  }
]
```

---

## Response Shapes

### ProjectMetadataDto
```json
{
  "projectMetadataId": 1,
  "targetType": "Project",
  "projectNumber": 12345,
  "offerId": null,
  "originalOfferId": null,
  "originalOfferNumber": null,
  "planningCategory": "Engineering",
  "planningStatus": "Active",
  "disciplineOwner": "MJ",
  "defaultDescription": "Main project",
  "colorTag": "#FF0000",
  "planningGroup": "Group A",
  "phase": "Execution",
  "probabilityPercent": 85.0,
  "budgetHours": 1200.0,
  "budgetRevenue": 500000.0,
  "lastPlanningReviewBy": "martin",
  "priority": 1,
  "isBillableForPlanning": true,
  "isAbsence": false,
  "isInternal": false,
  "isProbableCase": false,
  "isVisibleInPlanner": true,
  "dailyPlanningEnabled": true,
  "startDate": "2025-01-01",
  "endDate": "2025-12-31",
  "createdBy": "system",
  "updatedBy": "martin",
  "createdAtUtc": "2025-06-01T00:00:00Z",
  "updatedAtUtc": "2025-06-10T00:00:00Z"
}
```

### ProjectLifecycleLogDto
```json
{
  "projectLifecycleLogId": 1001,
  "targetType": "Project",
  "projectNumber": 12345,
  "offerId": null,
  "eventType": "StatusChanged",
  "eventTitle": "Status updated",
  "eventDescription": "Changed from Active to Closed",
  "oldValue": "Active",
  "newValue": "Closed",
  "snapshotJson": null,
  "createdBy": "martin",
  "createdAtUtc": "2025-06-10T12:00:00Z"
}
```

### ResourcePlanEntryDto
```json
{
  "resourcePlanEntryId": 5,
  "resourcePlanId": 7,
  "planningTargetId": 42,
  "planningTargetCode": "T1234",
  "planningTargetName": "Project Alpha",
  "planningTargetType": "Project",
  "projectNumber": 12345,
  "offerId": null,
  "internalPlanningCodeId": null,
  "planDate": "2025-06-15",
  "hours": 8.0,
  "description": "Design work",
  "isManualOverride": false,
  "createdBy": "martin",
  "updatedBy": null,
  "createdAt": "2025-06-15T08:00:00Z",
  "updatedAt": null
}
```
