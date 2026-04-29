# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-04-28 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/001-document-upload-management/spec.md`

## Summary

Adds a complete document upload and management system to ContosoDashboard. Users can upload files (up to 25 MB), organize them by category and project, share them with teammates, attach them to tasks, and download or preview them. An immutable activity log supports auditing. All file I/O is abstracted behind `IFileStorageService` so local filesystem storage can be swapped for Azure Blob Storage with a single DI registration change.

Technical approach: new Blazor Server page (`Documents.razor`) + a `ControllerBase` endpoint (`DocumentsController`) for file streaming outside `wwwroot`. Four new EF Core entities (`Document`, `DocumentShare`, `DocumentActivityLog`, `TaskDocument`). Four new services (`IDocumentService`, `IFileStorageService`, `IVirusScanService`, `IScanQueueService`) following the existing Pages→Services→Models layering. Async background scanning is abstracted behind `IScanQueueService`; the training implementation is a no-op stub; Azure Functions + Azure Queue Storage is the documented production swap-in.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0  
**Primary Dependencies**: Blazor Server 8.0, Entity Framework Core 8.0 (SQL Server), Bootstrap 5.3, Bootstrap Icons  
**Storage**: SQL Server LocalDB (`Server=(localdb)\mssqllocaldb;Database=ContosoDashboard`); files on local filesystem at `{ContentRoot}/AppData/uploads/`  
**Testing**: No automated test framework currently configured in the project (per Constitution V: Simplicity, no test framework is added as part of this feature)  
**Target Platform**: Windows developer workstation (Blazor Server; browser-rendered)  
**Project Type**: Single Blazor Server project  
**Performance Goals**: Upload ≤ 25 MB; download/preview served synchronously via `FileStreamResult`; no throughput requirements defined in spec  
**Constraints**: Offline-capable (all infrastructure local, no cloud dependencies); cookie auth only; SQL Server LocalDB; files served outside `wwwroot` via authorized controller  
**Scale/Scope**: Single-tenant, multi-user dashboard; document count bounded by disk space; no concurrency quota defined

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — all gates pass.*

Verify each principle from `.specify/memory/constitution.md` v1.0.0:

- [x] **I. Spec-Driven Development** — `specs/001-document-upload-management/spec.md` exists with 9 user stories (P6 added for background scan processing), 51 FRs (FR-044–FR-051 added), acceptance criteria, and 5 clarification Q&As. No implementation code has been written.
- [x] **II. Offline-First / Abstraction** — `IFileStorageService`, `IVirusScanService`, and `IScanQueueService` are interfaces; all training implementations (`LocalFileStorageService`, `StubVirusScanService`, `NoOpScanQueueService`) work entirely offline. Azure Blob Storage and Azure Functions + Queue Storage are production swap-ins requiring only DI registration changes in `Program.cs`.
- [x] **III. Security-Conscious Development** — `DocumentsController` carries `[Authorize]`; `Documents.razor` carries `@attribute [Authorize]`; `IDocumentService` enforces IDOR checks on every method (owner/member/role validation). Download path traversal is guarded in `LocalFileStorageService` via `Path.GetFullPath` comparison against the root.
- [x] **IV. Separation of Concerns** — Pages call `IDocumentService` only; `DocumentService` owns all business logic; `LocalFileStorageService` owns I/O; `ApplicationDbContext` is the data layer. No business logic in Pages or Models.
- [x] **V. Simplicity** — Two justified deviations are documented in the Complexity Tracking table below. Tags stored as comma-separated string (no tag entity). No client-side Blazor, no SignalR for uploads. The background scan worker is a minimal hosted service (`IHostedService`) that polls the DB every 30 s for `UnscannedPendingReview` documents and calls `IScanQueueService` — no additional framework needed.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-management/
├── plan.md              ← this file
├── research.md          ← Phase 0 complete
├── data-model.md        ← Phase 1 complete
├── quickstart.md        ← Phase 1 complete
├── contracts/
│   ├── IDocumentService.md
│   ├── IFileStorageService.md
│   ├── IVirusScanService.md  (also documents DocumentsController HTTP contract)
│   └── IScanQueueService.md
└── tasks.md             ← created by /speckit.tasks (not yet created)
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Controllers/
│   └── DocumentsController.cs        [NEW] Download/preview endpoints
├── Models/
│   ├── Document.cs                   [NEW]
│   ├── DocumentShare.cs              [NEW]
│   ├── DocumentActivityLog.cs        [NEW]
│   ├── TaskDocument.cs               [NEW]
│   └── Notification.cs               [MODIFIED] +3 NotificationType values
├── Services/
│   ├── DocumentService.cs            [NEW] IDocumentService implementation
│   ├── FileStorageService.cs         [NEW] IFileStorageService + LocalFileStorageService
│   ├── VirusScanService.cs           [NEW] IVirusScanService + StubVirusScanService
│   ├── ScanQueueService.cs           [NEW] IScanQueueService + NoOpScanQueueService
│   ├── ScanProcessorHostedService.cs [NEW] IHostedService — polls UnscannedPendingReview, dispatches to IScanQueueService
│   └── DashboardService.cs           [MODIFIED] +TotalDocuments + GetRecentDocumentsAsync
├── Pages/
│   └── Documents.razor               [NEW] /documents route
├── Shared/
│   ├── UploadDocumentModal.razor     [NEW] Upload modal component
│   └── NavMenu.razor                 [MODIFIED] +Documents nav link
├── Data/
│   └── ApplicationDbContext.cs       [MODIFIED] +4 DbSets + OnModelCreating
├── Program.cs                        [MODIFIED] +3 DI registrations + MapControllers
└── AppData/
    └── uploads/                      [NEW RUNTIME] file storage root (gitignored)
```

**Structure Decision**: Single Blazor Server project. A `Controllers/` subfolder is added — the standard ASP.NET Core pattern for mixing a `ControllerBase` endpoint into a Blazor/Razor Pages app. All new files follow the existing `Models/` → `Services/` → `Pages/` layering. The background scan processor is a hosted service registered via `builder.Services.AddHostedService<ScanProcessorHostedService>()`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Adding `ControllerBase` endpoint + `app.MapControllers()` to a Blazor Server app | Files stored outside `wwwroot` cannot be served by Blazor components; a `ControllerBase` action with `[Authorize]` is the standard ASP.NET Core pattern for authorized file streaming | Storing files inside `wwwroot` would expose them without authorization checks; Blazor `OnGet` / JS interop file serving is non-standard and harder to maintain |
| Adding `IHostedService` background worker (`ScanProcessorHostedService`) | FR-046–FR-050 require async processing of `UnscannedPendingReview` documents after upload; synchronous inline scanning would block the upload response and is unreliable | A cron-style external process would require additional infrastructure; `IHostedService` is built into ASP.NET Core and requires no additional packages |
