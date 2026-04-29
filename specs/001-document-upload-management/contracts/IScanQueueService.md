# Service Contract: IScanQueueService

**Layer**: Infrastructure (abstraction)  
**Namespace**: `ContosoDashboard.Services`  
**File**: `ContosoDashboard/Services/ScanQueueService.cs`

## Purpose

Abstracts the mechanism for enqueuing documents that require background virus scanning. Called by `ScanProcessorHostedService` after it identifies `UnscannedPendingReview` documents. No page or other service references this interface directly.

The interface ensures the feature is offline-capable: the default training implementation is a no-op. Azure Functions + Azure Queue Storage is the production swap-in, requiring only a DI registration change.

---

## Interface

```csharp
public interface IScanQueueService
{
    /// <summary>
    /// Enqueues a document for background virus scanning.
    /// No-ops if the service is unavailable or not configured.
    /// </summary>
    Task EnqueueAsync(int documentId);
}
```

---

## Training Implementation: `NoOpScanQueueService`

Does nothing. Returns immediately. `ScanProcessorHostedService` invokes `IVirusScanService.ScanAsync` in-process instead (synchronous stub path — acceptable for training because `StubVirusScanService` always returns `Clean`).

```csharp
public class NoOpScanQueueService : IScanQueueService
{
    public Task EnqueueAsync(int documentId) => Task.CompletedTask;
}
```

---

## Background Processor: `ScanProcessorHostedService`

**File**: `ContosoDashboard/Services/ScanProcessorHostedService.cs`  
**Type**: `BackgroundService` (`IHostedService` via `AddHostedService<>`)  
**Poll interval**: 30 seconds

### Workflow (per poll cycle)

```
1. Query Documents WHERE ScanStatus = "UnscannedPendingReview" AND IsDeleted = false
2. For each document:
   a. Open file stream via IFileStorageService.DownloadAsync
   b. Call IVirusScanService.ScanAsync
   c. If Clean  → set ScanStatus = "Clean"; log DocumentActivityLog Action = "Scan"
   d. If Malicious →
        - set IsDeleted = true; set ScanStatus = "Malicious"
        - delete all DocumentShare rows for this document
        - create notifications (NotificationType.DocumentRemovedFromShare) for each former recipient
        - create notification for uploader (custom message: "Your uploaded file was found to contain malicious content and has been removed.")
        - log DocumentActivityLog Action = "Delete" Details = "Malicious file detected"
   e. If Unavailable → leave status unchanged; retry on next poll cycle
3. SaveChangesAsync
```

### DI Registration (Program.cs addition)

```csharp
builder.Services.AddScoped<IScanQueueService, NoOpScanQueueService>();
builder.Services.AddHostedService<ScanProcessorHostedService>();
```

---

## Production Swap: Azure Functions + Azure Queue Storage

When deploying to Azure, swap the registration and provide an `AzureQueueScanQueueService` that enqueues a message to an Azure Storage Queue. An Azure Function with a `QueueTrigger` dequeues the message, calls the actual scan API, and calls back to the app (or updates the database directly if it has DB access).

```csharp
// Program.cs swap (production only):
builder.Services.AddScoped<IScanQueueService, AzureQueueScanQueueService>();
// builder.Services.AddHostedService<ScanProcessorHostedService>(); // can be removed if Azure Function handles processing
```

**No changes to `DocumentService`, `IDocumentService`, `IVirusScanService`, pages, or the database schema are required for this swap.**

---

## Failure / Error Behavior

| Scenario | Behavior |
|----------|----------|
| File missing from storage on scan | Log `Details = "File not found during scan"`, leave `ScanStatus = "UnscannedPendingReview"` for retry |
| `IVirusScanService` throws unexpectedly | Catch exception, log to `DocumentActivityLog`, leave status unchanged for retry |
| DB save fails mid-cycle | Log error; document remains `UnscannedPendingReview`; retry on next poll |
