# Service Contract: IFileStorageService

**Layer**: Infrastructure (abstraction)  
**Namespace**: `ContosoDashboard.Services`  
**File**: `ContosoDashboard/Services/FileStorageService.cs`

## Purpose

Abstracts all file I/O operations. `IDocumentService` calls this interface; no page or other service references it directly. Swapping to `AzureBlobStorageService` requires only a DI registration change in `Program.cs`.

---

## Interface

```csharp
public interface IFileStorageService
{
    /// <summary>
    /// Stores file at the given relative path. Returns the stored path.
    /// Caller constructs path as: {userId}/{projectId-or-personal}/{guid}.{ext}
    /// </summary>
    Task<string> UploadAsync(Stream fileStream, string relativePath, string contentType);

    /// <summary>
    /// Permanently removes the file at the given relative path.
    /// No-ops if file does not exist.
    /// </summary>
    Task DeleteAsync(string relativePath);

    /// <summary>
    /// Returns a readable stream for the file at the given path.
    /// Caller is responsible for disposing the stream.
    /// </summary>
    Task<Stream> DownloadAsync(string relativePath);
}
```

---

## Local Implementation: `LocalFileStorageService`

- **Root directory**: `{IWebHostEnvironment.ContentRootPath}/AppData/uploads/`
- **Path construction**: appends `relativePath` to root; uses `Path.GetFullPath` to guard against traversal
- **Path traversal guard**: Verify resolved path starts with the root directory before any I/O
- **Directory creation**: `Directory.CreateDirectory` on parent before writing
- **Error mapping**: `IOException` propagates as-is; `DocumentService` catches and maps to user error

## Azure Migration Notes

Swap registration in `Program.cs`:
```csharp
// Training:
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// Production:
builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
```

`AzureBlobStorageService` will use `Azure.Storage.Blobs.BlobContainerClient`. The same `relativePath` pattern works as a blob name. No changes to `DocumentService`, pages, or database schema required.
