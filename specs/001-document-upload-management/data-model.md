# Data Model: Document Upload and Management

**Feature**: 001-document-upload-management  
**Branch**: `001-document-upload-management`  
**Date**: 2026-04-28

## Overview

Five new entities are introduced. All use integer primary keys (consistent with existing `User`, `Project`, `TaskItem` tables). All are registered in `ApplicationDbContext`. No existing entities are modified except via additive navigation properties and enum extensions.

---

## Entity: Document

**Table**: `Documents`  
**Purpose**: Core record for an uploaded file and its metadata.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `DocumentId` | `int` | PK, identity | Auto-increment; consistent with existing int PKs |
| `Title` | `string` | Required, MaxLength(255) | User-provided; shown in all list views |
| `Description` | `string?` | MaxLength(2000) | Optional |
| `Category` | `string` | Required, MaxLength(100) | Stores text value: "Project Documents", "Personal Files", etc. (per stakeholder doc constraint) |
| `Tags` | `string?` | MaxLength(1000) | Comma-separated tag values; searched with `Contains` |
| `OriginalFileName` | `string` | Required, MaxLength(255) | Original filename as uploaded (for display only) |
| `StoredFilePath` | `string` | Required, MaxLength(500) | Relative path: `{userId}/{projectId-or-personal}/{guid}.{ext}` |
| `FileType` | `string` | Required, MaxLength(255) | MIME type, e.g. `application/vnd.openxmlformats-officedocument.wordprocessingml.document` (255 chars per stakeholder doc) |
| `FileSizeBytes` | `long` | Required | Stored in bytes; displayed as KB/MB in UI |
| `UploadedByUserId` | `int` | Required, FK → Users | IDOR check anchor |
| `ProjectId` | `int?` | Nullable FK → Projects | Null = personal document |
| `UploadedAt` | `DateTime` | Required | UTC; auto-set on insert |
| `ScanStatus` | `string` | Required, MaxLength(50), Default: "Clean" | Values: `"Clean"`, `"UnscannedPendingReview"`, `"Malicious"` |
| `IsDeleted` | `bool` | Required, Default: false | Soft-delete flag for cascade-share revocation; hard-delete via migration script or admin tool. Note: soft-delete is NOT exposed as "trash" to users — document is treated as deleted from all user views immediately. |

**Navigation Properties**:
- `UploadedByUser` → `User`
- `Project` → `Project?`
- `Shares` → `ICollection<DocumentShare>`
- `ActivityLogs` → `ICollection<DocumentActivityLog>`
- `TaskDocuments` → `ICollection<TaskDocument>`

**Indexes**:
- `(UploadedByUserId)` — My Documents query
- `(ProjectId)` — Project Documents query
- `(UploadedAt DESC)` — Recent Documents widget
- `(ScanStatus)` — Admin unscanned documents view

**Predefined Category Values** (enforced at service layer, not as a DB enum):
```
"Project Documents"
"Team Resources"
"Personal Files"
"Reports"
"Presentations"
"Other"
```

---

## Entity: DocumentShare

**Table**: `DocumentShares`  
**Purpose**: Records that a document has been shared with a specific user.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `DocumentShareId` | `int` | PK, identity | |
| `DocumentId` | `int` | Required, FK → Documents | |
| `SharedWithUserId` | `int` | Required, FK → Users | Recipient |
| `SharedByUserId` | `int` | Required, FK → Users | Owner who shared |
| `SharedAt` | `DateTime` | Required | UTC |

**Navigation Properties**:
- `Document` → `Document`
- `SharedWithUser` → `User`
- `SharedByUser` → `User`

**Indexes**:
- `(SharedWithUserId)` — "Shared with Me" tab query
- `(DocumentId)` — share revocation on delete

**Delete behavior**: When `Document.IsDeleted = true`, all associated `DocumentShare` rows are cascade-deleted (configured in `OnModelCreating`).

---

## Entity: DocumentActivityLog

**Table**: `DocumentActivityLogs`  
**Purpose**: Immutable audit trail of all document events.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `ActivityLogId` | `int` | PK, identity | |
| `DocumentId` | `int` | Required, FK → Documents (no cascade delete — keep log even after document deleted) | |
| `ActorUserId` | `int` | Required, FK → Users | Who performed the action |
| `Action` | `string` | Required, MaxLength(50) | Values: `"Upload"`, `"Download"`, `"Delete"`, `"Share"`, `"EditMetadata"`, `"ReplaceFile"` |
| `OccurredAt` | `DateTime` | Required | UTC |
| `Details` | `string?` | MaxLength(500) | Optional context (e.g., shared-with user name) |

**Navigation Properties**:
- `Document` → `Document`
- `ActorUser` → `User`

**Indexes**:
- `(DocumentId)` — per-document history
- `(ActorUserId)` — per-user activity report
- `(OccurredAt DESC)` — admin activity log view

---

## Entity: TaskDocument

**Table**: `TaskDocuments`  
**Purpose**: Join table linking a `TaskItem` to a `Document` (many-to-many attachment).

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `TaskDocumentId` | `int` | PK, identity | |
| `TaskId` | `int` | Required, FK → Tasks | |
| `DocumentId` | `int` | Required, FK → Documents | |
| `AttachedAt` | `DateTime` | Required | UTC |
| `AttachedByUserId` | `int` | Required, FK → Users | |

**Navigation Properties**:
- `Task` → `TaskItem`
- `Document` → `Document`
- `AttachedByUser` → `User`

**Indexes**:
- `(TaskId)` — task detail document list

---

## Additive Changes to Existing Entities

### `NotificationType` enum (Models/Notification.cs)

Add three values (backward-compatible):

```
DocumentShared          // FR-030: document shared with user
DocumentRemovedFromShare // FR-028: shared document was deleted
DocumentAddedToProject   // FR-037: new document added to user's project
```

### `DashboardSummary` class (Services/DashboardService.cs)

Add one property:

```csharp
public int TotalDocuments { get; set; }
```

### `ApplicationDbContext` (Data/ApplicationDbContext.cs)

Add four `DbSet<>` properties and configure relationships in `OnModelCreating`.

---

## New Service Interfaces (not entities, but complement the data model)

### `IFileStorageService`

```csharp
Task<string> UploadAsync(Stream fileStream, string filePath, string contentType);
Task DeleteAsync(string filePath);
Task<Stream> DownloadAsync(string filePath);
```

Implemented by `LocalFileStorageService` (stores files in `{ContentRoot}/AppData/uploads/`).

### `IVirusScanService`

```csharp
Task<ScanResult> ScanAsync(Stream fileStream, string fileName);
```

`ScanResult`: enum `{ Clean, Malicious, Unavailable }`.  
Implemented by `StubVirusScanService` (always returns `Clean`).

### `IScanQueueService`

```csharp
Task EnqueueAsync(int documentId);
```

Abstracts the background scan trigger. Training implementation: `NoOpScanQueueService` (no-op). Production swap-in: `AzureQueueScanQueueService` (enqueues to Azure Storage Queue; processed by Azure Function). See [contracts/IScanQueueService.md](contracts/IScanQueueService.md) for the full background processor workflow (`ScanProcessorHostedService`).

---

## EF Core Configuration Summary (OnModelCreating additions)

```
DocumentShare → Document: Cascade delete
DocumentShare → SharedWithUser: Restrict (preserve user record)
DocumentShare → SharedByUser: Restrict
DocumentActivityLog → Document: Restrict (keep log after document deleted)
DocumentActivityLog → ActorUser: Restrict
TaskDocument → Task: Cascade delete
TaskDocument → Document: Restrict (detach from task, not deleted)
Unique index: DocumentShare (DocumentId, SharedWithUserId) — prevent duplicate shares
```

---

## Entity Relationship Diagram

```
Users ──────────────────────────────────────────────────────────┐
  │                                                             │
  │ (UploadedByUserId)                                          │
  ▼                                                             │
Documents ──────────────────────────────────────────────────────┤
  │         │             │              │                      │
  │         │ (ProjectId) │              │                      │
  │         ▼             │              │                      │
  │      Projects         │              │                      │
  │                       │              │                      │
  │         (DocumentId)  │              │ (DocumentId)         │
  ▼                       │              ▼                      │
DocumentShares            │       DocumentActivityLogs          │
  │                       │                                     │
  │ (SharedWithUserId)     │ (TaskId)                           │
  ▼                       ▼                                     │
Users                  TaskDocuments ──────────────────────────►│
                          │                                    Users
                          ▼
                       TaskItems
```
