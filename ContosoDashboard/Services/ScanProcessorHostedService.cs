using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public class ScanProcessorHostedService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ScanProcessorHostedService> _logger;
    private PeriodicTimer? _timer;
    private Task? _executingTask;
    private CancellationTokenSource? _stoppingCts;

    public ScanProcessorHostedService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ScanProcessorHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scan Processor Hosted Service is starting");
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = DoWorkAsync(_stoppingCts.Token);
        return Task.CompletedTask;
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scan Processor background task is running");

        try
        {
            while (!stoppingToken.IsCancellationRequested && _timer != null)
            {
                await _timer.WaitForNextTickAsync(stoppingToken);

                _logger.LogDebug("Scan Processor executing scan cycle");

                try
                {
                    await ProcessUnscannedDocumentsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during scan cycle");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scan Processor background task is stopping due to cancellation");
        }
    }

    private async Task ProcessUnscannedDocumentsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var virusScanService = scope.ServiceProvider.GetRequiredService<IVirusScanService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        // Query documents pending scan
        var unscannedDocs = await db.Documents
            .Where(d => !d.IsDeleted && d.ScanStatus == "UnscannedPendingReview")
            .Include(d => d.UploadedByUser)
            .ToListAsync(stoppingToken);

        if (unscannedDocs.Count == 0)
        {
            _logger.LogDebug("No unscanned documents found");
            return;
        }

        _logger.LogInformation("Processing {Count} unscanned document(s)", unscannedDocs.Count);

        foreach (var doc in unscannedDocs)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation("Scanning document {DocumentId}: {Title}", doc.DocumentId, doc.Title);

                var scanResult = await virusScanService.ScanFileAsync(doc.StoredFilePath);

                if (scanResult == ScanResult.Clean)
                {
                    // Mark as clean
                    doc.ScanStatus = "Clean";
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Document {DocumentId} marked as Clean", doc.DocumentId);

                    // Notify owner
                    await notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = doc.UploadedByUserId,
                        Title = "Document Scan Complete",
                        Message = $"Your document '{doc.Title}' has been scanned and is clean.",
                        Type = NotificationType.SystemAnnouncement,
                        Priority = NotificationPriority.Informational
                    });
                }
                else if (scanResult == ScanResult.Malicious)
                {
                    // Delete malicious document
                    _logger.LogWarning("Document {DocumentId} detected as MALICIOUS. Deleting.", doc.DocumentId);

                    doc.IsDeleted = true;
                    doc.ScanStatus = "Malicious";
                    await db.SaveChangesAsync(stoppingToken);

                    // Notify owner
                    await notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = doc.UploadedByUserId,
                        Title = "Document Removed - Security Threat",
                        Message = $"Your document '{doc.Title}' was detected as malicious and has been removed for security reasons.",
                        Type = NotificationType.SystemAnnouncement,
                        Priority = NotificationPriority.Urgent
                    });

                    // Log activity (ActorUserId = 0 for system actions)
                    db.DocumentActivityLogs.Add(new DocumentActivityLog
                    {
                        DocumentId = doc.DocumentId,
                        ActorUserId = 0, // System action
                        Action = "DeleteMalicious",
                        Details = "Document automatically deleted after virus scan detected malicious content",
                        OccurredAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync(stoppingToken);
                }
                else if (scanResult == ScanResult.Unavailable)
                {
                    // Scanner unavailable, leave as pending
                    _logger.LogWarning("Virus scanner unavailable for document {DocumentId}", doc.DocumentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning document {DocumentId}", doc.DocumentId);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scan Processor Hosted Service is stopping");

        if (_executingTask == null)
        {
            return;
        }

        try
        {
            _stoppingCts?.Cancel();
        }
        finally
        {
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    public void Dispose()
    {
        _stoppingCts?.Cancel();
        _timer?.Dispose();
        _stoppingCts?.Dispose();
    }
}
