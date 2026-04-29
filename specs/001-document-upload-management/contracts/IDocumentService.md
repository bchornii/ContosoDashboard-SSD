# Service Contract: IDocumentService

**Layer**: Service  
**Namespace**: `ContosoDashboard.Services`  
**File**: `ContosoDashboard/Services/DocumentService.cs`

## Purpose

Orchestrates all document business logic: validation, authorization (IDOR), file storage coordination, notification dispatch, and activity logging. Pages and components MUST NOT call `IFileStorageService`, `IVirusScanService`, or the database directly for document operations — all access goes through `IDocumentService`.

---

## Interface

```csharp
public interface IDocumentService
{
    // Upload
    Task<DocumentUploadResult> UploadDocumentAsync(DocumentUploadRequest request, int requestingUserId);

    // Browse
    Task<List<Document>> GetMyDocumentsAsync(int requestingUserId, DocumentFilter? filter = null);
    Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
    Task<List<Document>> GetSharedWithMeAsync(int requestingUserId);
    Task<List<Document>> GetRecentDocumentsAsync(int requestingUserId, int count = 5);
    Task<int> GetDocumentCountAsync(int requestingUserId);

    // Detail / Download
    Task<Document?> GetDocumentAsync(int documentId, int requestingUserId);
    Task<DocumentDownloadResult?> DownloadDocumentAsync(int documentId, int requestingUserId);

    // Search
    Task<List<Document>> SearchDocumentsAsync(string query, int requestingUserId);

    // Edit
    Task<bool> UpdateDocumentMetadataAsync(int documentId, DocumentMetadataUpdate update, int requestingUserId);
    Task<bool> ReplaceDocumentFileAsync(int documentId, DocumentUploadRequest newFile, int requestingUserId);

    // Delete
    Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId);

    // Share
    Task<bool> ShareDocumentAsync(int documentId, int shareWithUserId, int requestingUserId);
    Task<bool> ShareDocumentWithProjectAsync(int documentId, int projectId, int requestingUserId);

    // Task attachment
    Task<bool> AttachDocumentToTaskAsync(int documentId, int taskId, int requestingUserId);
    Task<List<Document>> GetTaskDocumentsAsync(int taskId, int requestingUserId);

    // Admin
    Task<List<Document>> GetUnscannedDocumentsAsync(int requestingUserId);
    Task<List<DocumentActivityLog>> GetActivityLogAsync(int requestingUserId, int? documentId = null, int page = 1, int pageSize = 50);
}
```

---

## Key DTOs

```csharp
public record DocumentUploadRequest(
    string Title,
    string? Description,
    string Category,
    string? Tags,           // comma-separated
    int? ProjectId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    Stream FileStream       // MemoryStream; caller owns lifetime
);

public record DocumentUploadResult(
    bool Success,
    int? DocumentId,
    string? ErrorMessage
);

public record DocumentMetadataUpdate(
    string Title,
    string? Description,
    string Category,
    string? Tags
);

public record DocumentDownloadResult(
    Stream FileStream,
    string ContentType,
    string OriginalFileName
);

public record DocumentFilter(
    string? Category = null,
    int? ProjectId = null,
    DateTime? UploadedAfter = null,
    DateTime? UploadedBefore = null,
    string? SortBy = null,       // "title" | "uploadDate" | "category" | "fileSize"
    bool SortDescending = true
);
```

---

## Authorization Rules (enforced in every method)

| Operation | Employee | Team Lead | Project Manager | Administrator |
|-----------|----------|-----------|-----------------|---------------|
| Upload personal | ✅ | ✅ | ✅ | ✅ |
| Upload to project | If member | If member | If member or manager | ✅ |
| View own documents | ✅ | ✅ | ✅ | ✅ |
| View project docs | If member | If member (project-scoped) | ✅ own projects | ✅ all |
| View shared-with-me | ✅ | ✅ | ✅ | ✅ |
| Download | If accessible | If accessible | If accessible | ✅ all |
| Edit metadata | Own docs only | Own docs only | Own docs only | ✅ all |
| Delete | Own docs only | Own docs only | Any in own projects | ✅ all |
| Share | Own docs only | Own docs only | Own docs only | ✅ all |
| View activity log | — | — | — | ✅ |

---

## Error / Exception Behavior

| Scenario | Behavior |
|----------|----------|
| File > 25 MB | Return `DocumentUploadResult(false, null, "File exceeds the 25 MB size limit.")` |
| Unsupported extension | Return `DocumentUploadResult(false, null, "File type not supported. Supported types: …")` |
| User not member of target project | Return `DocumentUploadResult(false, null, "You are not a member of this project.")` |
| IDOR: document not accessible | Return `null` from `GetDocumentAsync`; `false` from mutation methods |
| Scan service unavailable | Upload succeeds; set `ScanStatus = "UnscannedPendingReview"` |
| Storage failure (`IOException`) | Return `DocumentUploadResult(false, null, "Upload failed. Please try again.")` |
