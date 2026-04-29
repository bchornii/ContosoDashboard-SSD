# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-upload-management/`
**Prerequisites**: plan.md ✅, spec.md ✅, data-model.md ✅, contracts/ ✅, research.md ✅, quickstart.md ✅

**Tests**: No automated test framework is configured in this project (per Constitution V: Simplicity). No test tasks are generated.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US9)
- All file paths are relative to repository root

## User Story → Phase Mapping

| Label | Story | Priority | Phase |
|-------|-------|----------|-------|
| US1 | Upload a Document | P1 | 3 |
| US2 | Browse and Filter Documents | P2 | 4 |
| US3 | Search for Documents | P3 | 5 |
| US4 | Download and Preview Documents | P3 | 6 |
| US5 | Edit, Replace, and Delete Documents | P4 | 7 |
| US6 | Share Documents | P4 | 8 |
| US7 | Task and Dashboard Integration | P5 | 9 |
| US8 | Administrator Audit and Reporting | P5 | 10 |
| US9 | Background Scan Processing | P6 | 11 |

---

## Phase 1: Setup

**Purpose**: Prepare runtime directory and gitignore so the app has a writable upload root from the first run.

- [X] T001 Create `ContosoDashboard/AppData/uploads/.gitkeep` and add `ContosoDashboard/AppData/uploads/` to the repository `.gitignore` so the upload directory exists at runtime but uploaded files are never committed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All new entities, DbContext changes, EF Core migration, service interfaces, infrastructure implementations, controller stub, and DI registrations that every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Create `Document` entity class with all properties (DocumentId, Title, Description, Category, Tags, OriginalFileName, StoredFilePath, FileType, FileSizeBytes, UploadedByUserId, ProjectId, UploadedAt, ScanStatus, IsDeleted) and navigation properties per data-model.md in `ContosoDashboard/Models/Document.cs`
- [X] T003 [P] Create `DocumentShare` entity class with all properties (DocumentShareId, DocumentId, SharedWithUserId, SharedByUserId, SharedAt) and navigation properties per data-model.md in `ContosoDashboard/Models/DocumentShare.cs`
- [X] T004 [P] Create `DocumentActivityLog` entity class with all properties (ActivityLogId, DocumentId, ActorUserId, Action, OccurredAt, Details) and navigation properties per data-model.md in `ContosoDashboard/Models/DocumentActivityLog.cs`
- [X] T005 [P] Create `TaskDocument` entity class with all properties (TaskDocumentId, TaskId, DocumentId, AttachedAt, AttachedByUserId) and navigation properties per data-model.md in `ContosoDashboard/Models/TaskDocument.cs`
- [X] T006 [P] Add three new `NotificationType` enum values (`DocumentShared`, `DocumentRemovedFromShare`, `DocumentAddedToProject`) to the existing `NotificationType` enum in `ContosoDashboard/Models/Notification.cs`
- [X] T007 Update `ApplicationDbContext` to add `DbSet<Document> Documents`, `DbSet<DocumentShare> DocumentShares`, `DbSet<DocumentActivityLog> DocumentActivityLogs`, `DbSet<TaskDocument> TaskDocuments`, and configure all relationships and cascade behaviors in `OnModelCreating` per data-model.md (DocumentShare→Document cascade delete; DocumentActivityLog→Document restrict; TaskDocument→Task cascade delete; all User FKs restrict) in `ContosoDashboard/Data/ApplicationDbContext.cs`
- [X] T008 Add EF Core database migration `AddDocumentTables` using `dotnet ef migrations add AddDocumentTables --project ContosoDashboard` and verify the generated migration includes all four new tables with indexes defined in data-model.md
- [X] T009 [P] Create the `IDocumentService` interface with all method signatures and all supporting DTOs (`DocumentUploadRequest`, `DocumentUploadResult`, `DocumentMetadataUpdate`, `DocumentDownloadResult`, `DocumentFilter`) in `ContosoDashboard/Services/DocumentService.cs`; leave the `DocumentService` class as a stub implementing the interface with `throw new NotImplementedException()` for each method
- [X] T010 [P] Create the `IFileStorageService` interface with `UploadAsync`, `DeleteAsync`, and `DownloadAsync` signatures, and implement `LocalFileStorageService` that stores files under `{IWebHostEnvironment.ContentRootPath}/AppData/uploads/` using `Path.GetFullPath` path-traversal guard (verify resolved path starts with root before any I/O), creates directories on demand, and propagates `IOException` in `ContosoDashboard/Services/FileStorageService.cs`
- [X] T011 [P] Create the `IVirusScanService` interface, the `ScanResult` enum (`Clean`, `Malicious`, `Unavailable`), and the `StubVirusScanService` implementation that always returns `ScanResult.Clean` without reading the stream in `ContosoDashboard/Services/VirusScanService.cs`
- [X] T012 [P] Create the `IScanQueueService` interface with `EnqueueAsync(int documentId)` and the `NoOpScanQueueService` implementation that returns `Task.CompletedTask` immediately in `ContosoDashboard/Services/ScanQueueService.cs`
- [X] T013 [P] Create `DocumentsController` inheriting `ControllerBase` with `[ApiController]`, `[Route("documents")]`, and `[Authorize]` attributes; add stub action methods for `GET download/{documentId}` and `GET preview/{documentId}` that return `NotFound()` as placeholders; inject `IDocumentService` via constructor in `ContosoDashboard/Controllers/DocumentsController.cs`
- [X] T014 Register `IDocumentService`→`DocumentService`, `IFileStorageService`→`LocalFileStorageService`, `IVirusScanService`→`StubVirusScanService`, and `IScanQueueService`→`NoOpScanQueueService` as scoped services; add `builder.Services.AddControllers()` after `AddRazorPages()`; add `app.MapControllers()` after `app.MapRazorPages()` in `ContosoDashboard/Program.cs`

**Checkpoint**: Foundation ready — all entities exist, migration generated, all interfaces defined, DI wired. User story phases can now proceed.

---

## Phase 3: User Story 1 — Upload a Document (Priority: P1) 🎯 MVP

**Goal**: Employees can upload files up to 25 MB with a title and category. Files are validated (size, extension), virus-scanned via stub, stored outside `wwwroot`, and recorded in the database. A success/error message is displayed.

**Independent Test**: Navigate to `/documents`, open the upload modal, select any PDF or image under 25 MB, fill in Title and Category, click Upload — the file appears in the My Documents list. Attempting to upload a file >25 MB or with an unsupported extension shows a clear error message.

- [X] T015 [US1] Implement `DocumentService.UploadDocumentAsync`: validate file size (≤25 MB), validate extension against the allowed list (pdf, docx, doc, xlsx, xls, pptx, ppt, txt, jpeg, jpg, png), call `IVirusScanService.ScanAsync` (fail-open: set `ScanStatus="UnscannedPendingReview"` when result is `Unavailable`, reject when `Malicious`), build `StoredFilePath` as `{userId}/{projectId-or-"personal"}/{guid}.{ext}`, call `IFileStorageService.UploadAsync`, save `Document` entity, and append a `DocumentActivityLog` entry with `Action="Upload"` in `ContosoDashboard/Services/DocumentService.cs`
- [X] T016 [P] [US1] Create `UploadDocumentModal.razor` shared component with an `IBrowserFile` input (`InputFile`), required Title and Category fields (Category rendered as a `<select>` with the six predefined values), optional Description, Project (dropdown of user's projects), and Tags fields, a progress indicator shown during upload, and success/error alert feedback; copy file content into a `MemoryStream` before calling `IDocumentService.UploadDocumentAsync`; use `@key` on `InputFile` to reset the picker after each upload in `ContosoDashboard/Shared/UploadDocumentModal.razor`
- [X] T017 [US1] Create `Documents.razor` Blazor page at route `/documents` with `@attribute [Authorize]`, a page title, an Upload Document button that opens `UploadDocumentModal`, and a placeholder document list section; inject `IDocumentService` and resolve `UserId` from `AuthenticationStateProvider` in `ContosoDashboard/Pages/Documents.razor`
- [X] T018 [P] [US1] Add a Documents navigation link (using Bootstrap Icons `bi-file-earmark-text`) to the nav menu after the existing Tasks link in `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: User Story 1 fully functional — upload works end-to-end, file is stored, document appears in the list.

---

## Phase 4: User Story 2 — Browse and Filter Documents (Priority: P2)

**Goal**: Users see all their uploaded documents in a My Documents tab with sort controls (title, upload date, category, file size) and filters (category, project, date range). Project members see project documents in the ProjectDetails page.

**Independent Test**: Upload several documents with different categories and projects. Verify sorting reorders the list correctly and filtering narrows results as expected. Navigate to a project — verify its documents section shows only that project's documents.

- [X] T019 [US2] Implement `DocumentService.GetMyDocumentsAsync` applying `DocumentFilter` (category match, projectId match, date range, sort by title/uploadDate/category/fileSize with ascending/descending) using EF Core LINQ on `Documents` where `UploadedByUserId == requestingUserId` and `IsDeleted == false` in `ContosoDashboard/Services/DocumentService.cs`
- [X] T020 [US2] Implement `DocumentService.GetProjectDocumentsAsync` querying `Documents` where `ProjectId == projectId` and `IsDeleted == false`, enforcing that `requestingUserId` is a member of the project (check `ProjectMembers` table) or is an Administrator in `ContosoDashboard/Services/DocumentService.cs`
- [X] T021 [US2] Add a tabbed layout to `Documents.razor` with a "My Documents" tab showing a table with columns Title, Category, Upload Date, File Size, Project; add sort toggle buttons for each column header and filter dropdowns (Category, Project, date range From/To); call `GetMyDocumentsAsync` on load and re-query on filter/sort change in `ContosoDashboard/Pages/Documents.razor`
- [X] T022 [US2] Add a "Documents" collapsible section to `ProjectDetails.razor` that calls `IDocumentService.GetProjectDocumentsAsync` for the current project and renders a simple table with Title, Category, Upload Date, and File Size columns in `ContosoDashboard/Pages/ProjectDetails.razor`

**Checkpoint**: User Story 2 functional — My Documents tab shows sorted/filtered list; project page shows project documents.

---

## Phase 5: User Story 3 — Search for Documents (Priority: P3)

**Goal**: Users type a keyword and receive matching documents (found by title, description, tags, uploader name, or project name) within 2 seconds. Only accessible documents are returned.

**Independent Test**: Upload documents with distinct titles, tags, and descriptions. Search for a keyword — verify only matching, accessible documents appear. Search for a term with no matches — verify the empty-state message is shown.

- [X] T023 [US3] Implement `DocumentService.SearchDocumentsAsync` building an EF Core query on `Documents` (joined with `Users` for uploader name and `Projects` for project name) using `EF.Functions.Like` or `Contains` on Title, Description, Tags, and the joined uploader's `FullName` and project's `Name`; exclude `IsDeleted == true` documents; enforce IDOR (own docs + shared-with-me + project membership + admin) in `ContosoDashboard/Services/DocumentService.cs`
- [X] T024 [US3] Add a search text input above the tab strip in `Documents.razor` that debounces input and calls `SearchDocumentsAsync`; display results in a dedicated results list replacing the tab content while a query is active; show a "No documents found" message when results are empty in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 3 functional — search returns matching accessible documents only.

---

## Phase 6: User Story 4 — Download and Preview Documents (Priority: P3)

**Goal**: Users with access can download any document. PDFs and images can be previewed inline in the browser. Unauthorized access is denied.

**Independent Test**: Upload a PDF, click preview — verify it renders inline in the browser tab. Click download on any document — verify the file is received intact. Attempt to access `/documents/download/{id}` for a document you don't own (not shared) — verify 403/404 response.

- [X] T025 [US4] Implement `DocumentService.GetDocumentAsync` (return `null` if `IsDeleted` or IDOR check fails) and `DocumentService.DownloadDocumentAsync` (call `IFileStorageService.DownloadAsync`, append `DocumentActivityLog` with `Action="Download"`, return `DocumentDownloadResult`) in `ContosoDashboard/Services/DocumentService.cs`
- [X] T026 [US4] Implement the `GET /documents/download/{documentId}` action returning `FileStreamResult` with `Content-Disposition: attachment; filename="{OriginalFileName}"` and the `GET /documents/preview/{documentId}` action returning `FileStreamResult` with `Content-Disposition: inline` (only for PDF and image content types; return `BadRequest` otherwise); both actions call `IDocumentService.DownloadDocumentAsync` and return `NotFound` if result is null in `ContosoDashboard/Controllers/DocumentsController.cs`
- [X] T027 [US4] Add a download icon button (link to `/documents/download/{id}`) and a preview icon button (link to `/documents/preview/{id}`, visible only for PDF/image file types) to each document row in `Documents.razor` and in the documents section of `ProjectDetails.razor` in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 4 functional — download delivers files; PDF/image preview opens inline; unauthorized requests are denied.

---

## Phase 7: User Story 5 — Edit, Replace, and Delete Documents (Priority: P4)

**Goal**: Document owners can edit metadata (title, description, category, tags) and replace the file. Owners and Project Managers can delete documents; deletion permanently removes the file, revokes all shares, and notifies recipients.

**Independent Test**: Upload a document, edit its title — verify the change is reflected in the list. Replace the file — verify the new file is downloadable and metadata is preserved. Delete the document — verify it no longer appears in any list.

- [X] T028 [US5] Implement `DocumentService.UpdateDocumentMetadataAsync` applying `DocumentMetadataUpdate` fields to the document entity; enforce owner-or-admin authorization; append `DocumentActivityLog` with `Action="EditMetadata"` in `ContosoDashboard/Services/DocumentService.cs`
- [X] T029 [US5] Implement `DocumentService.ReplaceDocumentFileAsync` calling `IFileStorageService.DeleteAsync` on the old `StoredFilePath`, building a new `StoredFilePath` with a fresh GUID, calling `IFileStorageService.UploadAsync` with the new stream, updating `Document.StoredFilePath` and `Document.FileSizeBytes`, and appending `DocumentActivityLog` with `Action="ReplaceFile"`; enforce owner-or-admin authorization in `ContosoDashboard/Services/DocumentService.cs`
- [X] T030 [US5] Implement `DocumentService.DeleteDocumentAsync` setting `Document.IsDeleted = true`, retrieving all `DocumentShare` rows for the document, sending `INotificationService.CreateNotificationAsync` with `NotificationType.DocumentRemovedFromShare` for each `SharedWithUserId`, removing the `DocumentShare` rows, calling `IFileStorageService.DeleteAsync`, and appending `DocumentActivityLog` with `Action="Delete"`; enforce owner, Project Manager for project documents, or admin authorization in `ContosoDashboard/Services/DocumentService.cs`
- [X] T031 [US5] Add an edit metadata inline form (rendered on row expand or small modal) with pre-filled Title, Description, Category, Tags fields and a Save button; add a Replace File upload input that accepts a new file and calls `ReplaceDocumentFileAsync`; add a Delete button that shows a JavaScript `confirm()` dialog before calling `DeleteDocumentAsync`; refresh the document list after each operation in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 5 functional — metadata edits persist; file replacement delivers the new version; deletion removes the document from all views.

---

## Phase 8: User Story 6 — Share Documents (Priority: P4)

**Goal**: Document owners share with specific users or project teams. Recipients see the document in a "Shared with Me" tab and receive an in-app notification.

**Independent Test**: Share a document with another user — log in as that user and verify the document appears in the Shared with Me tab and an unread notification exists. Share with a project team — verify all project members can see the document in Shared with Me.

- [X] T032 [US6] Implement `DocumentService.ShareDocumentAsync` creating a `DocumentShare` record, calling `INotificationService.CreateNotificationAsync` with `NotificationType.DocumentShared` for `shareWithUserId`; if the document is project-associated also send `NotificationType.DocumentAddedToProject` to all other project members not already shared with; enforce owner-or-admin authorization in `ContosoDashboard/Services/DocumentService.cs`
- [X] T033 [US6] Implement `DocumentService.ShareDocumentWithProjectAsync` iterating over all `ProjectMembers` for the given project (excluding the document owner), calling `ShareDocumentAsync` for each member who does not already have a `DocumentShare` for this document in `ContosoDashboard/Services/DocumentService.cs`
- [X] T034 [US6] Implement `DocumentService.GetSharedWithMeAsync` querying `DocumentShares` where `SharedWithUserId == requestingUserId`, joining to `Documents`, and filtering out documents where `IsDeleted == true` in `ContosoDashboard/Services/DocumentService.cs`
- [X] T035 [US6] Add a "Shared with Me" tab to `Documents.razor` that calls `GetSharedWithMeAsync` and renders a list with Title, Category, Shared By, Shared At, and Download/Preview links; add a Share button to each document row in the My Documents tab that opens an inline user-search picker (text input filtered against `IUserService`) and calls `ShareDocumentAsync` on selection in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 6 functional — sharing creates visible entries in Shared with Me tab; notifications are delivered.

---

## Phase 9: User Story 7 — Task and Dashboard Integration (Priority: P5)

**Goal**: Users attach existing documents to tasks from the task detail page. The dashboard home page shows a Recent Documents widget (last 5 uploads) and a document count summary card.

**Independent Test**: Open a task, attach an existing document — verify it appears in the Related Documents section. Navigate to the dashboard — verify the Recent Documents widget shows the 5 most recently uploaded documents and the summary card shows the correct document count.

- [X] T036 [US7] Implement `DocumentService.AttachDocumentToTaskAsync` creating a `TaskDocument` join record (validate that `requestingUserId` has access to both the task and the document); implement `DocumentService.GetTaskDocumentsAsync` querying `TaskDocuments` where `TaskId == taskId`, joining to `Documents`, filtering out `IsDeleted` documents, and enforcing that `requestingUserId` is a task assignee or project member in `ContosoDashboard/Services/DocumentService.cs`
- [X] T037 [US7] Implement `DocumentService.GetRecentDocumentsAsync` querying `Documents` for the requesting user (own + shared-with-me) ordered by `UploadedAt DESC`, limited to `count` results and excluding `IsDeleted`; implement `DocumentService.GetDocumentCountAsync` returning the total count of accessible non-deleted documents for `requestingUserId` in `ContosoDashboard/Services/DocumentService.cs`
- [X] T038 [P] [US7] Add `public int TotalDocuments { get; set; }` to `DashboardSummary` and implement `GetRecentDocumentsAsync(int userId, int count = 5)` on `DashboardService` calling `IDocumentService.GetRecentDocumentsAsync` and populating `TotalDocuments` via `IDocumentService.GetDocumentCountAsync` in `ContosoDashboard/Services/DashboardService.cs`
- [X] T039 [P] [US7] Add a "Related Documents" collapsible section to `Tasks.razor` (task detail view) that calls `IDocumentService.GetTaskDocumentsAsync` for the selected task and renders document title/category/download link rows; add an "Attach Document" button that opens `UploadDocumentModal` pre-filled with the task's project in `ContosoDashboard/Pages/Tasks.razor`
- [X] T040 [US7] Add a "Recent Documents" widget card to `Index.razor` that renders the 5 most recently uploaded document titles (each linking to `/documents`); update the existing summary statistics section to include a Documents count card sourced from `DashboardSummary.TotalDocuments` in `ContosoDashboard/Pages/Index.razor`

**Checkpoint**: User Story 7 functional — documents attach to tasks; dashboard widget and count card reflect uploaded documents.

---

## Phase 10: User Story 8 — Administrator Audit and Reporting (Priority: P5)

**Goal**: Administrators can view all document activity (uploads, downloads, deletions, shares) and see a report of most uploaded document types, most active uploaders, and unscanned/malicious documents.

**Independent Test**: Log in as `admin@contoso.com`, navigate to Documents → Activity Log tab — verify all upload, download, delete, and share events from previous operations appear. Verify the unscanned documents list shows documents with `ScanStatus = "UnscannedPendingReview"`.

- [X] T041 [US8] Implement `DocumentService.GetUnscannedDocumentsAsync` querying `Documents` where `ScanStatus != "Clean"` and `IsDeleted == false`, enforcing admin role; implement `DocumentService.GetActivityLogAsync` querying `DocumentActivityLogs` with optional `documentId` filter, paginated by `page`/`pageSize`, ordered by `OccurredAt DESC`, joining uploader and actor user names in `ContosoDashboard/Services/DocumentService.cs`
- [X] T042 [US8] Add an "Activity Log" tab to `Documents.razor` visible only when the user's role is Administrator; render a paginated table of log entries (Document Title, Actor, Action, Date, Details); add an "Unscanned Documents" sub-section listing documents returned by `GetUnscannedDocumentsAsync`; add a simple reporting summary (top 5 file types by count, top 5 uploaders by count) computed from the activity log query results in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 8 functional — administrators can see the full audit trail and unscanned document list.

---

## Phase 11: User Story 9 — Background Scan Processing (Priority: P6)

**Goal**: A background worker polls every 30 seconds for `UnscannedPendingReview` documents, calls the scan service, marks clean documents as `"Clean"`, and soft-deletes malicious documents while revoking shares and notifying the uploader.

**Independent Test**: Manually set a document's `ScanStatus` to `"UnscannedPendingReview"` in the database. Wait up to 30 seconds and verify the `ScanProcessorHostedService` updates the status to `"Clean"` (using the stub scanner). Verify that a document manually marked `Malicious` results in `IsDeleted=true`, all `DocumentShare` rows removed, and an in-app notification for the uploader.

- [X] T043 Create `ScanProcessorHostedService` inheriting `BackgroundService` with a `ExecuteAsync` loop that uses `PeriodicTimer` with a 30-second interval; inject `IServiceScopeFactory` (to create scoped `IDocumentService`, `IFileStorageService`, `IVirusScanService`, and `ApplicationDbContext` inside each poll cycle) in `ContosoDashboard/Services/ScanProcessorHostedService.cs`
- [X] T044 Implement the scan cycle body in `ScanProcessorHostedService.ExecuteAsync`: query `Documents` where `ScanStatus = "UnscannedPendingReview"` and `IsDeleted = false`; for each document call `IFileStorageService.DownloadAsync` then `IVirusScanService.ScanAsync`; on `Clean` set `ScanStatus = "Clean"` and append `DocumentActivityLog Action="Scan"`; on `Malicious` set `IsDeleted = true` and `ScanStatus = "Malicious"`, delete all `DocumentShare` rows, send `INotificationService.CreateNotificationAsync(NotificationType.DocumentRemovedFromShare)` for each former recipient and for the uploader, append `DocumentActivityLog Action="Delete" Details="Malicious file detected"`; on `Unavailable` leave status unchanged; wrap per-document logic in try/catch and append a `DocumentActivityLog Details="Error during scan"` on unexpected exceptions; call `SaveChangesAsync` at end of cycle in `ContosoDashboard/Services/ScanProcessorHostedService.cs`
- [X] T045 Register `ScanProcessorHostedService` with `builder.Services.AddHostedService<ScanProcessorHostedService>()` in `ContosoDashboard/Program.cs`

**Checkpoint**: User Story 9 functional — background scan cycle processes pending documents; malicious files are removed and recipients notified.

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Security hardening verification, scan-status UI flag, and end-to-end quickstart validation.

- [X] T046 [P] Verify the path-traversal guard in `LocalFileStorageService.UploadAsync` and `DownloadAsync`: add an assertion that `Path.GetFullPath(resolvedPath).StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)` and throw `InvalidOperationException` if the check fails; also confirm `[Authorize]` is present on `DocumentsController` and `@attribute [Authorize]` is present on `Documents.razor` in `ContosoDashboard/Services/FileStorageService.cs`
- [X] T047 Add a visible badge or warning label (e.g., Bootstrap `badge bg-warning`) to document rows where `ScanStatus == "UnscannedPendingReview"` in `Documents.razor`, `ProjectDetails.razor`, and the Shared with Me tab so users are clearly informed about unscanned documents per FR-005a in `ContosoDashboard/Pages/Documents.razor`
- [X] T048 Run end-to-end validation per `specs/001-document-upload-management/quickstart.md` Steps 1–10: start the app, upload a document, browse/filter/sort, download, preview a PDF, share with another user, attach to a task, verify dashboard widget, test deletion and share revocation, and verify admin activity log

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories**
- **User Stories (Phases 3–11)**: All depend on Phase 2 completion; stories can proceed sequentially in priority order
- **Polish (Phase 12)**: Depends on all desired user story phases being complete

### User Story Dependencies

| Story | Depends On | Notes |
|-------|-----------|-------|
| US1 — Upload (P1) | Phase 2 | No story dependencies |
| US2 — Browse/Filter (P2) | Phase 2 | Requires US1 data to test, but independently implementable |
| US3 — Search (P3) | Phase 2 | Independently implementable; test using US1-uploaded docs |
| US4 — Download/Preview (P3) | Phase 2 | Independently implementable; test using US1-uploaded docs |
| US5 — Edit/Delete (P4) | Phase 2 | Requires US1 (documents must exist to edit/delete) |
| US6 — Share (P4) | Phase 2 | Requires US1 (documents must exist to share) |
| US7 — Task/Dashboard (P5) | Phase 2 | Requires US1; Tasks.razor section needs documents |
| US8 — Admin Audit (P5) | Phase 2 | Requires US1–US6 to populate audit log meaningfully |
| US9 — Background Scan (P6) | Phase 2 + US1 | Background processor depends on upload creating UnscannedPendingReview records |

### Within Each User Story

- Service implementation tasks before UI tasks
- Interface/model tasks in Phase 2 before all service tasks in Phase 3+
- Core implementation before downstream integration

### Parallel Opportunities

- **Phase 2**: T002, T003, T004, T005, T006, T009, T010, T011, T012, T013 can all run in parallel (10 tasks — all create independent new files)
- **Phase 3 (US1)**: T015 (service) and T016 (modal component) and T018 (nav menu) can run in parallel after Phase 2
- **Phase 9 (US7)**: T038 (DashboardService) and T039 (Tasks.razor) can run in parallel after T036 and T037 complete

---

## Parallel Example: Phase 2 Foundational

```
┌─ T002 Document.cs
├─ T003 DocumentShare.cs
├─ T004 DocumentActivityLog.cs        ← all parallel
├─ T005 TaskDocument.cs
├─ T006 Notification.cs (enum extend)
├─ T009 IDocumentService + DTOs
├─ T010 IFileStorageService + LocalFileStorageService
├─ T011 IVirusScanService + Stub
├─ T012 IScanQueueService + NoOp
└─ T013 DocumentsController (stub)
        ↓ (all complete)
   T007 ApplicationDbContext (requires T002–T005)
        ↓
   T008 EF Migration (requires T007)
        ↓
   T014 Program.cs registrations (requires T007–T013)
```

## Parallel Example: User Story 1 (Phase 3)

```
        Phase 2 complete
        ↓
┌─ T015 DocumentService.UploadDocumentAsync
├─ T016 UploadDocumentModal.razor     ← parallel
└─ T018 NavMenu.razor                 ← parallel
        ↓ (T015 + T016 complete)
   T017 Documents.razor (requires T015 + T016)
```

---

## Implementation Strategy

### MVP Scope (Recommended first delivery)

Complete **Phase 1 + Phase 2 + Phase 3 (US1)** to deliver a working document upload with:
- Files stored securely outside `wwwroot`
- Virus scan stub (fail-open)
- Upload modal accessible from the Documents page
- Document appears in a basic list after upload

This alone delivers SC-001 progress and validates the entire infrastructure stack.

### Incremental Delivery

| Increment | Phases | Value Delivered |
|-----------|--------|-----------------|
| MVP | 1–3 | File upload works end-to-end |
| +Browse | 4 | My Documents list with sort/filter |
| +Search | 5 | Keyword search across documents |
| +Access | 6 | Download and inline preview |
| +Lifecycle | 7–8 | Edit, delete, share |
| +Integration | 9–10 | Task attachments, dashboard widget |
| +Admin | 11 | Audit log and reporting |
| +BgScan | 12 | Background scan processing |
| Polish | 13 | Security hardening and quickstart validation |

---

## Summary

| Metric | Count |
|--------|-------|
| **Total tasks** | 48 |
| Setup (Phase 1) | 1 |
| Foundational (Phase 2) | 13 |
| US1 — Upload (Phase 3) | 4 |
| US2 — Browse/Filter (Phase 4) | 4 |
| US3 — Search (Phase 5) | 2 |
| US4 — Download/Preview (Phase 6) | 3 |
| US5 — Edit/Replace/Delete (Phase 7) | 4 |
| US6 — Share (Phase 8) | 4 |
| US7 — Task/Dashboard (Phase 9) | 5 |
| US8 — Admin Audit (Phase 10) | 2 |
| US9 — Background Scan (Phase 11) | 3 |
| Polish (Phase 12) | 3 |
| **Parallelizable tasks [P]** | 17 |
| **Parallel clusters** | 3 |
| **User stories covered** | 9 |
| **No test tasks** | (no test framework configured) |

