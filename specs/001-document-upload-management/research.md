# Research: Document Upload and Management

**Feature**: 001-document-upload-management  
**Branch**: `001-document-upload-management`  
**Date**: 2026-04-28

## Summary

All NEEDS CLARIFICATION items were resolved by inspecting the existing codebase directly. No external research was required. All decisions align with established patterns in the ContosoDashboard codebase and the project constitution.

---

## Resolution 1: File Upload in Blazor Server

**Decision**: Use `InputFile` component with `MemoryStream` copy pattern; stream file contents into memory before processing; clear `IBrowserFile` reference after copy.

**Rationale**: Blazor Server's `IBrowserFile` streams are tied to the connection lifetime and can be disposed before async processing completes. The MemoryStream pattern (already documented in the stakeholder spec) is the established safe approach. Matches `@using Microsoft.AspNetCore.Components.Forms` already available in the project.

**Alternatives considered**:
- Direct `IBrowserFile.OpenReadStream()` piped to disk — rejected: stream can be disposed mid-pipe if the Blazor circuit disconnects.
- Streaming directly to `IFileStorageService.UploadAsync()` — rejected: safe only when stream lifetime is fully controlled; adds complexity for error recovery.

**Key implementation note**: Use `@key` on `InputFile` component to force component re-creation after upload, ensuring the file picker is reset correctly.

---

## Resolution 2: File Storage Interface Pattern

**Decision**: Define `IFileStorageService` with `UploadAsync`, `DeleteAsync`, `DownloadAsync`, `GetUrlAsync`; implement `LocalFileStorageService` using `System.IO`; register via DI in `Program.cs`.

**Rationale**: The constitution mandates infrastructure abstraction (Principle II). All existing infrastructure services (`IProjectService`, `INotificationService`, etc.) follow the interface-in-same-file pattern used throughout the codebase. `LocalFileStorageService` stores files under `AppData/uploads/{userId}/{projectId-or-personal}/{guid}.{ext}` — outside `wwwroot` for security.

**Alternatives considered**:
- Static file serving from `wwwroot` — rejected: allows unauthenticated access; violates FR-012 and Principle III.
- Named pipe pattern with streaming controller — rejected: over-engineered for training; adds unnecessary HTTP controller to a Blazor Server app.

**File path pattern**: `{userId}/{projectId or "personal"}/{guid}.{ext}` — portable as blob name for future Azure migration.

---

## Resolution 3: Download/Preview Serving via Controller Endpoint

**Decision**: Add a minimal `DocumentsController` (inheriting `ControllerBase`) with `[Authorize]` attribute and service-level IDOR check to serve file downloads and previews. This is the only Razor/MVC controller needed.

**Rationale**: Files stored outside `wwwroot` cannot be served as static files; they require an authorized endpoint. Blazor Server apps support mixed Razor/MVC usage — `Program.cs` already calls `app.MapRazorPages()`. Adding `app.MapControllers()` is a minimal change. PDF/image preview uses the same endpoint with `inline` content-disposition.

**Alternatives considered**:
- Blazor page serving file bytes via `JSInterop` — rejected: creates large in-memory payloads transferred over SignalR; not suitable for 25 MB files.
- Using `IFileResult` from Blazor pages — rejected: not supported; Blazor pages cannot return file responses.

---

## Resolution 4: Tag Storage Model

**Decision**: Store tags as a serialized comma-separated string column on the `Document` entity rather than a separate `Tag` table with many-to-many join.

**Rationale**: The spec requires tag-based search (FR-018) but does not require tag management (browse all tags, rename tags, etc.). A flat string column keeps the schema minimal (constitution Principle V: YAGNI). EF Core queries can use `EF.Functions.Like` or `Contains` for tag search. Tag management is explicitly out of scope.

**Alternatives considered**:
- Separate `DocumentTag` join table — rejected: adds two tables and a join for no additional user-visible capability given the out-of-scope constraint on tag management.
- JSON column — rejected: LocalDB / SQL Server 2019+ supports JSON but adds complexity without benefit at training scale.

---

## Resolution 5: Virus Scan Stub (Fail-Open, Clarification Q1)

**Decision**: Implement `IVirusScanService` with a single `ScanAsync(Stream) → ScanResult` method. Training implementation (`StubVirusScanService`) always returns `ScanResult.Clean`. When the scan service throws an exception, the upload proceeds with `ScanStatus = "UnscannedPendingReview"` on the document record.

**Rationale**: Stakeholder clarification (Q1) chose fail-open. An interface stub keeps the training implementation simple while making the integration point explicit for future replacement (e.g., Windows Defender ATP, ClamAV). The `Document` entity needs a `ScanStatus` field to support FR-005a.

**Alternatives considered**:
- Inline boolean `IsScanned` flag — rejected: insufficient to represent the three states needed (clean, malicious, pending); `ScanStatus` string or enum is more expressive.
- No abstraction; inline stub — rejected: violates Principle II (infrastructure must be abstracted).

---

## Resolution 6: Document Activity Log Storage

**Decision**: Add a `DocumentActivityLog` entity in the existing `ApplicationDbContext`, tracked via a new `DbSet<DocumentActivityLog>`. Log entries are insert-only; no update/delete operations on the log table.

**Rationale**: The existing pattern stores all data in `ApplicationDbContext`. A separate log store (e.g., file-based logging) would diverge from the established pattern and make admin reporting (FR-039) harder to implement. EF Core `SaveChanges` with a service-layer insert call matches all existing service patterns.

**Alternatives considered**:
- Serilog structured logging to file — rejected: not queryable for report generation (FR-039); requires additional tooling.
- Separate audit database — rejected: over-engineered for training; constitution Principle V.

---

## Resolution 7: NotificationType Extension

**Decision**: Extend the existing `NotificationType` enum with three new values: `DocumentShared`, `DocumentRemovedFromShare`, `DocumentAddedToProject`. Use the existing `INotificationService.CreateNotificationAsync` method; no new notification infrastructure needed.

**Rationale**: The `NotificationType` enum already has 7 values (TaskAssignment, TaskUpdate, TaskDueSoon, TaskCompleted, TaskComment, ProjectUpdate, SystemAnnouncement). Adding document notification types is a backward-compatible additive change. All existing notification consumers filter by type or display generically.

**Alternatives considered**:
- Separate `DocumentNotificationService` — rejected: unnecessary wrapper; `INotificationService` is general-purpose.
- Reuse `ProjectUpdate` type — rejected: conflates distinct notification semantics; breaks admin reporting.

---

## Resolution 8: DashboardService Extension

**Decision**: Add `GetRecentDocumentsAsync(int userId, int count = 5)` and a `DocumentCount` property to `DashboardSummary` in the existing `IDashboardService` / `DashboardService`. Index.razor calls these alongside existing summary data.

**Rationale**: `DashboardSummary` is a plain C# record/class already populated by `DashboardService`. Adding two fields is the minimal change consistent with Principle V. `Index.razor` already injects `IDashboardService`.

**Alternatives considered**:
- New `IDocumentDashboardService` — rejected: unnecessary indirection for two fields.
- Calling `IDocumentService` directly from `Index.razor` — rejected: violates Principle IV (pages call services, but adding a second service just for the dashboard widget conflates concerns that should stay in the dashboard service).

---

## Resolution 9: Project Documents Tab

**Decision**: Add a "Documents" tab to `ProjectDetails.razor` that queries documents by `ProjectId`. Reuse the upload modal component (FR-032a) with project pre-filled.

**Rationale**: `ProjectDetails.razor` already uses a tabbed layout pattern (or can adopt one). Adding a Documents tab is consistent with the clarified navigation model (Q3) and keeps project-related documents co-located with project detail.

**Alternatives considered**:
- Separate `/project/{id}/documents` route — rejected: unnecessary page split; adds route complexity without benefit.

---

## Resolution 10: Search Implementation

**Decision**: Implement search as an EF Core LINQ query with `Contains` / `EF.Functions.Like` across title, description, tags (string contains), uploader display name, and project name. Apply user-visibility filter before executing.

**Rationale**: At training scale (up to 500 documents per user per spec SC-006), a SQL `LIKE` search on indexed columns satisfies the 2-second requirement (FR-019). Full-text search (SQL Server FTS) is available but would require additional index setup beyond the training scope.

**Alternatives considered**:
- In-memory LINQ after loading all accessible documents — rejected: does not scale to 500 documents within 2 seconds.
- SQL Server Full-Text Search — rejected: requires `CREATE FULLTEXT INDEX` DDL beyond EF Core migration; YAGNI at training scale.

---

## Unresolved / Deferred Items

| Item | Disposition |
|------|-------------|
| Storage-full error handling | Deferred to implementation: catch `IOException` in `LocalFileStorageService.UploadAsync`, surface as `ServiceException` to `DocumentService`, display error message in upload modal. No spec change needed. |
| User removed from project mid-session | Covered by existing assumption in spec: user loses project-scoped access, personal uploads remain. IDOR check in `DocumentService.GetDocumentAsync` enforces this at query time. |
| Simultaneous duplicate filename uploads | GUID-based file paths (per stakeholder doc) guarantee uniqueness at the filesystem level. Database `DocumentId` is identity-generated. No collision possible. |
| Content-type mismatch (renamed .exe) | Deferred to implementation: validate both file extension (whitelist) AND content-type header in `DocumentService`. Log mismatch as a security event in `DocumentActivityLog`. |
