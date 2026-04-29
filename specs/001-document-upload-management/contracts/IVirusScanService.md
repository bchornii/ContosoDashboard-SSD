# Service Contract: IVirusScanService

**Layer**: Infrastructure (abstraction)  
**Namespace**: `ContosoDashboard.Services`  
**File**: `ContosoDashboard/Services/VirusScanService.cs`

## Purpose

Abstracts virus/malware scanning of uploaded file streams before they are stored or made accessible. Called by `IDocumentService.UploadDocumentAsync` immediately after receiving the file stream.

---

## Interface

```csharp
public interface IVirusScanService
{
    /// <summary>
    /// Scans the provided stream.
    /// Stream position is reset to 0 before scanning; caller must reset position before further use.
    /// Returns ScanResult.Unavailable if the scan service is unreachable — caller applies fail-open policy.
    /// </summary>
    Task<ScanResult> ScanAsync(Stream fileStream, string fileName);
}

public enum ScanResult
{
    Clean,
    Malicious,
    Unavailable  // scan service unreachable; caller applies fail-open policy
}
```

---

## Training Implementation: `StubVirusScanService`

Always returns `ScanResult.Clean`. Does not read the stream. Registered via DI.

## Fail-Open Policy (resolved in Clarification Q1)

In `DocumentService.UploadDocumentAsync`:
```
if (scanResult == ScanResult.Malicious)  → reject upload
if (scanResult == ScanResult.Unavailable) → allow upload; set Document.ScanStatus = "UnscannedPendingReview"
if (scanResult == ScanResult.Clean)       → allow upload; set Document.ScanStatus = "Clean"
```

## DI Registration

```csharp
// Training (stub):
builder.Services.AddScoped<IVirusScanService, StubVirusScanService>();

// Production swap (e.g., ClamAV, Defender ATP):
builder.Services.AddScoped<IVirusScanService, ClamAvVirusScanService>();
```

## HTTP Controller Contract: DocumentsController

**File**: `ContosoDashboard/Controllers/DocumentsController.cs`  
**Purpose**: Serves file downloads and previews from outside `wwwroot`. Only endpoint requiring `ControllerBase` in this application.

```
GET /documents/download/{documentId}
  [Authorize] attribute
  → service-level IDOR check
  → Content-Disposition: attachment; filename="{OriginalFileName}"
  → Returns FileStreamResult

GET /documents/preview/{documentId}
  [Authorize] attribute
  → service-level IDOR check (PDF and image types only)
  → Content-Disposition: inline
  → Returns FileStreamResult
```

`Program.cs` additions required:
```csharp
builder.Services.AddControllers();   // after AddRazorPages()
app.MapControllers();                // after app.MapRazorPages()
```
