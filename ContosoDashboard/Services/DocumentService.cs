using ContosoDashboard.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services
{
    // ─── DTOs ───────────────────────────────────────────────────────────────

    public class DocumentUploadRequest
    {
        public IBrowserFile File { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Tags { get; set; }
        public int? ProjectId { get; set; }
    }

    public class DocumentUploadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Document? Document { get; set; }
    }

    public class DocumentMetadataUpdate
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Tags { get; set; }
    }

    public class DocumentDownloadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Stream? FileStream { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public class DocumentFilter
    {
        public string? SearchTerm { get; set; }
        public string? Category { get; set; }
        public int? ProjectId { get; set; }
        public string? FileType { get; set; }
        public DateTime? UploadedAfter { get; set; }
        public DateTime? UploadedBefore { get; set; }
        public bool IncludeSharedWithMe { get; set; } = true;
    }

    // ─── Interface ──────────────────────────────────────────────────────────

    public interface IDocumentService
    {
        Task<DocumentUploadResult> UploadDocumentAsync(int uploadedByUserId, DocumentUploadRequest request);
        Task<List<Document>> GetUserDocumentsAsync(int userId, DocumentFilter? filter = null);
        Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
        Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId);
        Task<DocumentDownloadResult> DownloadDocumentAsync(int documentId, int requestingUserId);
        Task<DocumentDownloadResult> GetPreviewAsync(int documentId, int requestingUserId);
        Task<bool> UpdateDocumentMetadataAsync(int documentId, int requestingUserId, DocumentMetadataUpdate update);
        Task<bool> ReplaceDocumentFileAsync(int documentId, int requestingUserId, IBrowserFile newFile);
        Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId);
        Task<bool> ShareDocumentAsync(int documentId, int sharedByUserId, int sharedWithUserId);
        Task<bool> ShareDocumentWithProjectAsync(int documentId, int projectId, int sharedByUserId);
        Task<bool> RemoveShareAsync(int documentId, int removedByUserId, int sharedWithUserId);
        Task<List<DocumentShare>> GetDocumentSharesAsync(int documentId, int requestingUserId);
        Task<List<Document>> GetSharedWithMeAsync(int userId);
        Task<List<Document>> SearchDocumentsAsync(int userId, string searchTerm);
        Task<bool> AttachToTaskAsync(int documentId, int taskId, int attachedByUserId);
        Task<bool> DetachFromTaskAsync(int documentId, int taskId, int requestingUserId);
        Task<List<Document>> GetTaskDocumentsAsync(int taskId, int requestingUserId);
        Task<int> GetDocumentCountAsync(int userId);
        Task<List<Document>> GetRecentDocumentsAsync(int userId, int count = 5);
        Task<List<Document>> GetAllDocumentsAsync();
        Task<List<Document>> GetUnscannedDocumentsAsync(int requestingUserId, int skip = 0, int take = 50);
        Task<List<DocumentActivityLog>> GetActivityLogAsync(int requestingUserId, int skip = 0, int take = 100);
        Task<bool> UpdateScanStatusAsync(int documentId, string scanStatus);
    }

    // ─── Implementation ─────────────────────────────────────────────────────

    public class DocumentService : IDocumentService
    {
        private readonly Data.ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly INotificationService _notificationService;
        private readonly ILogger<DocumentService> _logger;
        private readonly IVirusScanService _virusScanService;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".jpeg", ".jpg", ".png"
        };

        private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

        public DocumentService(
            Data.ApplicationDbContext db,
            IFileStorageService fileStorage,
            INotificationService notificationService,
            ILogger<DocumentService> logger,
            IVirusScanService virusScanService)
        {
            _db = db;
            _fileStorage = fileStorage;
            _notificationService = notificationService;
            _logger = logger;
            _virusScanService = virusScanService;
        }

        public async Task<DocumentUploadResult> UploadDocumentAsync(int uploadedByUserId, DocumentUploadRequest request)
        {
            if (request.File.Size > MaxFileSizeBytes)
                return new DocumentUploadResult { Success = false, ErrorMessage = "File exceeds the 25 MB size limit." };

            var ext = Path.GetExtension(request.File.Name);
            if (!AllowedExtensions.Contains(ext))
                return new DocumentUploadResult { Success = false, ErrorMessage = $"File type '{ext}' is not allowed." };

            string storedPath;
            try
            {
                storedPath = await _fileStorage.SaveFileAsync(request.File, uploadedByUserId, request.ProjectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save uploaded file for user {UserId}", uploadedByUserId);
                return new DocumentUploadResult { Success = false, ErrorMessage = "Failed to store the file. Please try again." };
            }

            // Perform virus scan
            string scanStatus;
            try
            {
                var scanResult = await _virusScanService.ScanFileAsync(storedPath);
                scanStatus = scanResult switch
                {
                    ScanResult.Clean => "Clean",
                    ScanResult.Malicious => "Malicious",
                    ScanResult.Unavailable => "UnscannedPendingReview",
                    _ => "UnscannedPendingReview"
                };

                if (scanResult == ScanResult.Malicious)
                {
                    await _fileStorage.DeleteFileAsync(storedPath);
                    return new DocumentUploadResult { Success = false, ErrorMessage = "The uploaded file failed security scanning and cannot be stored." };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Virus scan failed for user {UserId}, file will be marked for review", uploadedByUserId);
                scanStatus = "UnscannedPendingReview";
            }

            var document = new Document
            {
                Title = request.Title,
                Description = request.Description,
                Category = request.Category,
                Tags = request.Tags,
                OriginalFileName = request.File.Name,
                StoredFilePath = storedPath,
                FileType = request.File.ContentType,
                FileSizeBytes = request.File.Size,
                UploadedByUserId = uploadedByUserId,
                ProjectId = request.ProjectId,
                UploadedAt = DateTime.UtcNow,
                ScanStatus = scanStatus
            };

            _db.Documents.Add(document);

            var log = new DocumentActivityLog
            {
                Document = document,
                ActorUserId = uploadedByUserId,
                Action = "Upload",
                OccurredAt = DateTime.UtcNow,
                Details = $"Uploaded '{request.File.Name}'"
            };
            _db.DocumentActivityLogs.Add(log);

            await _db.SaveChangesAsync();
            return new DocumentUploadResult { Success = true, Document = document };
        }

        public async Task<List<Document>> GetUserDocumentsAsync(int userId, DocumentFilter? filter = null)
        {
            var query = _db.Documents
                .Where(d => !d.IsDeleted && (d.UploadedByUserId == userId ||
                    _db.DocumentShares.Any(s => s.DocumentId == d.DocumentId && s.SharedWithUserId == userId)));

            if (filter != null)
                query = ApplyFilter(query, filter);

            return await query.OrderByDescending(d => d.UploadedAt).ToListAsync();
        }

        public async Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
        {
            // Check if user is admin
            var user = await _db.Users.FindAsync(requestingUserId);
            bool isAdmin = user?.Role == UserRole.Administrator;

            // Check if user is a project member
            bool isMember = await _db.ProjectMembers.AnyAsync(pm =>
                pm.ProjectId == projectId && pm.UserId == requestingUserId);

            if (!isAdmin && !isMember)
                return new List<Document>();

            return await _db.Documents
                .Include(d => d.UploadedByUser)
                .Where(d => !d.IsDeleted && d.ProjectId == projectId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId)
        {
            var doc = await _db.Documents
                .Include(d => d.UploadedByUser)
                .Include(d => d.Project)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);

            if (doc == null) return null;

            bool canAccess = doc.UploadedByUserId == requestingUserId ||
                await _db.DocumentShares.AnyAsync(s => s.DocumentId == documentId && s.SharedWithUserId == requestingUserId);

            return canAccess ? doc : null;
        }

        public async Task<DocumentDownloadResult> DownloadDocumentAsync(int documentId, int requestingUserId)
        {
            var doc = await GetDocumentByIdAsync(documentId, requestingUserId);
            if (doc == null)
                return new DocumentDownloadResult { Success = false, ErrorMessage = "Document not found or access denied." };

            try
            {
                var stream = await _fileStorage.ReadFileAsync(doc.StoredFilePath);
                await LogActivityAsync(documentId, requestingUserId, "Download");
                return new DocumentDownloadResult
                {
                    Success = true,
                    FileStream = stream,
                    FileName = doc.OriginalFileName,
                    ContentType = doc.FileType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read file for document {DocumentId}", documentId);
                return new DocumentDownloadResult { Success = false, ErrorMessage = "Failed to read the file." };
            }
        }

        public async Task<DocumentDownloadResult> GetPreviewAsync(int documentId, int requestingUserId)
        {
            // For MVP, preview uses same stream as download
            return await DownloadDocumentAsync(documentId, requestingUserId);
        }

        public async Task<bool> UpdateDocumentMetadataAsync(int documentId, int requestingUserId, DocumentMetadataUpdate update)
        {
            var doc = await _db.Documents
                .Include(d => d.UploadedByUser)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);
            if (doc == null) return false;

            // Authorize: owner or admin
            if (doc.UploadedByUserId != requestingUserId && doc.UploadedByUser.Role != UserRole.Administrator)
            {
                var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == requestingUserId);
                if (requestingUser?.Role != UserRole.Administrator) return false;
            }

            doc.Title = update.Title;
            doc.Description = update.Description;
            doc.Category = update.Category;
            doc.Tags = update.Tags;

            await LogActivityAsync(documentId, requestingUserId, "EditMetadata", "Metadata updated");
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReplaceDocumentFileAsync(int documentId, int requestingUserId, IBrowserFile newFile)
        {
            var doc = await _db.Documents
                .Include(d => d.UploadedByUser)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);
            if (doc == null) return false;

            // Authorize: owner or admin
            if (doc.UploadedByUserId != requestingUserId)
            {
                var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == requestingUserId);
                if (requestingUser?.Role != UserRole.Administrator) return false;
            }

            // Validate new file
            var extension = Path.GetExtension(newFile.Name).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                _logger.LogWarning("File type {Extension} not allowed for document replacement", extension);
                return false;
            }

            if (newFile.Size > MaxFileSizeBytes)
            {
                _logger.LogWarning("File size {Size} exceeds maximum {MaxSize} for document replacement", newFile.Size, MaxFileSizeBytes);
                return false;
            }

            // Delete old file
            try
            {
                await _fileStorage.DeleteFileAsync(doc.StoredFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old file at {Path}, continuing with replacement", doc.StoredFilePath);
            }

            // Save new file
            var newStoredPath = await _fileStorage.SaveFileAsync(newFile, doc.UploadedByUserId, doc.ProjectId);

            // Scan new file
            var scanResult = await _virusScanService.ScanFileAsync(newStoredPath);
            if (scanResult == ScanResult.Malicious)
            {
                _logger.LogWarning("Malicious file detected during replacement for document {DocumentId}, deleting", documentId);
                await _fileStorage.DeleteFileAsync(newStoredPath);
                return false;
            }

            // Update document entity
            doc.StoredFilePath = newStoredPath;
            doc.FileSizeBytes = newFile.Size;
            doc.OriginalFileName = newFile.Name;
            doc.FileType = newFile.ContentType;
            doc.ScanStatus = scanResult == ScanResult.Clean ? "Clean" : "UnscannedPendingReview";

            await LogActivityAsync(documentId, requestingUserId, "ReplaceFile", $"File replaced with {newFile.Name}");
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId)
        {
            var doc = await _db.Documents
                .Include(d => d.UploadedByUser)
                .Include(d => d.Project)
                    .ThenInclude(p => p!.ProjectMembers)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);
            if (doc == null) return false;

            // Authorize: owner OR ProjectManager (for project docs) OR admin
            bool isOwner = doc.UploadedByUserId == requestingUserId;
            var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == requestingUserId);
            bool isAdmin = requestingUser?.Role == UserRole.Administrator;
            bool isProjectManager = false;

            if (doc.ProjectId.HasValue && requestingUser != null)
            {
                isProjectManager = requestingUser.Role == UserRole.ProjectManager &&
                    doc.Project!.ProjectMembers.Any(pm => pm.UserId == requestingUserId);
            }

            if (!isOwner && !isProjectManager && !isAdmin) return false;

            // Soft delete
            doc.IsDeleted = true;

            // Get all shares and notify users
            var shares = await _db.DocumentShares
                .Where(s => s.DocumentId == documentId)
                .ToListAsync();

            foreach (var share in shares)
            {
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = share.SharedWithUserId,
                    Title = "Document Removed",
                    Message = $"The document \"{doc.Title}\" has been deleted and is no longer shared with you.",
                    Type = NotificationType.DocumentRemovedFromShare,
                    Priority = NotificationPriority.Informational,
                    IsRead = false,
                    CreatedDate = DateTime.UtcNow
                });
            }

            // Remove all shares
            _db.DocumentShares.RemoveRange(shares);

            // Optional: Delete physical file (commented out to preserve files for audit/recovery)
            // try
            // {
            //     await _fileStorage.DeleteFileAsync(doc.StoredFilePath);
            // }
            // catch (Exception ex)
            // {
            //     _logger.LogWarning(ex, "Failed to delete physical file at {Path}", doc.StoredFilePath);
            // }

            await LogActivityAsync(documentId, requestingUserId, "Delete", $"Document deleted by {requestingUser?.DisplayName}");
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ShareDocumentAsync(int documentId, int sharedByUserId, int sharedWithUserId)
        {
            var doc = await _db.Documents
                .Include(d => d.Project)
                    .ThenInclude(p => p!.ProjectMembers)
                .Include(d => d.UploadedByUser)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);
            if (doc == null) return false;

            // Authorize: owner or admin
            if (doc.UploadedByUserId != sharedByUserId)
            {
                var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == sharedByUserId);
                if (requestingUser?.Role != UserRole.Administrator) return false;
            }

            bool alreadyShared = await _db.DocumentShares.AnyAsync(s =>
                s.DocumentId == documentId && s.SharedWithUserId == sharedWithUserId);
            if (alreadyShared) return true;

            _db.DocumentShares.Add(new DocumentShare
            {
                DocumentId = documentId,
                SharedByUserId = sharedByUserId,
                SharedWithUserId = sharedWithUserId,
                SharedAt = DateTime.UtcNow
            });

            await LogActivityAsync(documentId, sharedByUserId, "Share", $"Shared with user {sharedWithUserId}");
            await _db.SaveChangesAsync();

            // Send notification to the user receiving the share
            await _notificationService.CreateNotificationAsync(new Notification
            {
                UserId = sharedWithUserId,
                Title = "Document shared with you",
                Message = $"'{doc.Title}' has been shared with you by {doc.UploadedByUser.DisplayName}.",
                Type = NotificationType.DocumentShared,
                Priority = NotificationPriority.Informational
            });

            // If project-associated document, notify other project members
            if (doc.ProjectId.HasValue && doc.Project != null)
            {
                var otherProjectMembers = doc.Project.ProjectMembers
                    .Where(pm => pm.UserId != doc.UploadedByUserId && pm.UserId != sharedWithUserId)
                    .ToList();

                foreach (var member in otherProjectMembers)
                {
                    // Check if not already shared
                    bool memberAlreadyHasAccess = await _db.DocumentShares.AnyAsync(s =>
                        s.DocumentId == documentId && s.SharedWithUserId == member.UserId);
                    
                    if (!memberAlreadyHasAccess)
                    {
                        await _notificationService.CreateNotificationAsync(new Notification
                        {
                            UserId = member.UserId,
                            Title = "Document added to project",
                            Message = $"'{doc.Title}' has been added to the project '{doc.Project.Name}'.",
                            Type = NotificationType.DocumentAddedToProject,
                            Priority = NotificationPriority.Informational
                        });
                    }
                }
            }

            return true;
        }

        public async Task<bool> ShareDocumentWithProjectAsync(int documentId, int projectId, int sharedByUserId)
        {
            var doc = await _db.Documents
                .Include(d => d.UploadedByUser)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);
            if (doc == null) return false;

            // Authorize: owner or admin
            if (doc.UploadedByUserId != sharedByUserId)
            {
                var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == sharedByUserId);
                if (requestingUser?.Role != UserRole.Administrator) return false;
            }

            // Get all project members except the document owner
            var projectMembers = await _db.ProjectMembers
                .Where(pm => pm.ProjectId == projectId && pm.UserId != doc.UploadedByUserId)
                .ToListAsync();

            int sharedCount = 0;
            foreach (var member in projectMembers)
            {
                // Check if not already shared
                bool alreadyShared = await _db.DocumentShares.AnyAsync(s =>
                    s.DocumentId == documentId && s.SharedWithUserId == member.UserId);
                
                if (!alreadyShared)
                {
                    var success = await ShareDocumentAsync(documentId, sharedByUserId, member.UserId);
                    if (success) sharedCount++;
                }
            }

            _logger.LogInformation("Shared document {DocumentId} with {Count} project members", documentId, sharedCount);
            return sharedCount > 0;
        }

        public async Task<bool> RemoveShareAsync(int documentId, int removedByUserId, int sharedWithUserId)
        {
            var share = await _db.DocumentShares.FirstOrDefaultAsync(s =>
                s.DocumentId == documentId && s.SharedWithUserId == sharedWithUserId);

            if (share == null) return false;

            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null || doc.UploadedByUserId != removedByUserId) return false;

            _db.DocumentShares.Remove(share);
            await LogActivityAsync(documentId, removedByUserId, "Unshare", $"Removed share for user {sharedWithUserId}");
            await _db.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(new Notification
            {
                UserId = sharedWithUserId,
                Title = "Document access removed",
                Message = $"Access to '{doc.Title}' has been removed.",
                Type = NotificationType.DocumentRemovedFromShare,
                Priority = NotificationPriority.Informational
            });

            return true;
        }

        public async Task<List<DocumentShare>> GetDocumentSharesAsync(int documentId, int requestingUserId)
        {
            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null || doc.UploadedByUserId != requestingUserId) return new List<DocumentShare>();

            return await _db.DocumentShares
                .Include(s => s.SharedWithUser)
                .Where(s => s.DocumentId == documentId)
                .ToListAsync();
        }

        public async Task<List<Document>> GetSharedWithMeAsync(int userId)
        {
            return await _db.DocumentShares
                .Where(s => s.SharedWithUserId == userId)
                .Include(s => s.Document)
                    .ThenInclude(d => d.UploadedByUser)
                .Where(s => !s.Document.IsDeleted)
                .Select(s => s.Document)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<List<Document>> SearchDocumentsAsync(int userId, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Document>();

            var user = await _db.Users.Include(u => u.ProjectMemberships).FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return new List<Document>();

            var pattern = $"%{searchTerm}%";

            // Build base query for accessible documents
            var query = _db.Documents
                .Include(d => d.UploadedByUser)
                .Include(d => d.Project)
                .Where(d => !d.IsDeleted);

            // Apply IDOR: own docs + shared + project membership + admin
            if (user.Role != UserRole.Administrator)
            {
                var projectIds = user.ProjectMemberships.Select(pm => pm.ProjectId).ToList();
                var sharedDocIds = _db.DocumentShares
                    .Where(s => s.SharedWithUserId == userId)
                    .Select(s => s.DocumentId)
                    .ToList();

                query = query.Where(d =>
                    d.UploadedByUserId == userId ||
                    sharedDocIds.Contains(d.DocumentId) ||
                    (d.ProjectId.HasValue && projectIds.Contains(d.ProjectId.Value))
                );
            }

            // Search across multiple fields
            query = query.Where(d =>
                EF.Functions.Like(d.Title, pattern) ||
                (d.Description != null && EF.Functions.Like(d.Description, pattern)) ||
                (d.Tags != null && EF.Functions.Like(d.Tags, pattern)) ||
                EF.Functions.Like(d.UploadedByUser.DisplayName, pattern) ||
                (d.Project != null && EF.Functions.Like(d.Project.Name, pattern))
            );

            return await query
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<bool> AttachToTaskAsync(int documentId, int taskId, int attachedByUserId)
        {
            var doc = await GetDocumentByIdAsync(documentId, attachedByUserId);
            if (doc == null) return false;

            bool alreadyAttached = await _db.TaskDocuments.AnyAsync(td =>
                td.DocumentId == documentId && td.TaskId == taskId);
            if (alreadyAttached) return true;

            _db.TaskDocuments.Add(new TaskDocument
            {
                DocumentId = documentId,
                TaskId = taskId,
                AttachedByUserId = attachedByUserId,
                AttachedAt = DateTime.UtcNow
            });

            await LogActivityAsync(documentId, attachedByUserId, "AttachToTask", $"Attached to task {taskId}");
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DetachFromTaskAsync(int documentId, int taskId, int requestingUserId)
        {
            var td = await _db.TaskDocuments.FirstOrDefaultAsync(x =>
                x.DocumentId == documentId && x.TaskId == taskId);
            if (td == null) return false;

            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null || doc.UploadedByUserId != requestingUserId) return false;

            _db.TaskDocuments.Remove(td);
            await LogActivityAsync(documentId, requestingUserId, "DetachFromTask", $"Detached from task {taskId}");
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<Document>> GetTaskDocumentsAsync(int taskId, int requestingUserId)
        {
            // Get the task with project info to check authorization
            var task = await _db.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p!.ProjectMembers)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
            
            if (task == null) return new List<Document>();

            var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == requestingUserId);
            if (requestingUser == null) return new List<Document>();

            // Authorize: task assignee OR project member OR admin
            bool isAssignee = task.AssignedUserId == requestingUserId;
            bool isProjectMember = task.ProjectId.HasValue && 
                task.Project!.ProjectMembers.Any(pm => pm.UserId == requestingUserId);
            bool isAdmin = requestingUser.Role == UserRole.Administrator;

            if (!isAssignee && !isProjectMember && !isAdmin)
                return new List<Document>();

            return await _db.TaskDocuments
                .Where(td => td.TaskId == taskId)
                .Select(td => td.Document)
                .Where(d => !d.IsDeleted)
                .Include(d => d.UploadedByUser)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<int> GetDocumentCountAsync(int userId)
        {
            var user = await _db.Users.Include(u => u.ProjectMemberships).FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return 0;

            if (user.Role == UserRole.Administrator)
            {
                return await _db.Documents.CountAsync(d => !d.IsDeleted);
            }

            var projectIds = user.ProjectMemberships.Select(pm => pm.ProjectId).ToList();
            var sharedDocIds = await _db.DocumentShares
                .Where(s => s.SharedWithUserId == userId)
                .Select(s => s.DocumentId)
                .ToListAsync();

            return await _db.Documents
                .Where(d => !d.IsDeleted && (
                    d.UploadedByUserId == userId ||
                    sharedDocIds.Contains(d.DocumentId) ||
                    (d.ProjectId.HasValue && projectIds.Contains(d.ProjectId.Value))
                ))
                .CountAsync();
        }

        public async Task<List<Document>> GetRecentDocumentsAsync(int userId, int count = 5)
        {
            return await _db.Documents
                .Where(d => !d.IsDeleted && (d.UploadedByUserId == userId ||
                    _db.DocumentShares.Any(s => s.DocumentId == d.DocumentId && s.SharedWithUserId == userId)))
                .OrderByDescending(d => d.UploadedAt)
                .Take(count)
                .Include(d => d.UploadedByUser)
                .ToListAsync();
        }

        public async Task<List<Document>> GetAllDocumentsAsync()
        {
            return await _db.Documents
                .Where(d => !d.IsDeleted)
                .Include(d => d.UploadedByUser)
                .Include(d => d.Project)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<List<Document>> GetUnscannedDocumentsAsync(int requestingUserId, int skip = 0, int take = 50)
        {
            // Verify admin access
            var user = await _db.Users.FindAsync(requestingUserId);
            if (user == null || user.Role != UserRole.Administrator)
                return new List<Document>();

            return await _db.Documents
                .Where(d => !d.IsDeleted && d.ScanStatus != "Clean")
                .Include(d => d.UploadedByUser)
                .Include(d => d.Project)
                .OrderByDescending(d => d.UploadedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<DocumentActivityLog>> GetActivityLogAsync(int requestingUserId, int skip = 0, int take = 100)
        {
            // Verify admin access
            var user = await _db.Users.FindAsync(requestingUserId);
            if (user == null || user.Role != UserRole.Administrator)
                return new List<DocumentActivityLog>();

            return await _db.DocumentActivityLogs
                .Include(log => log.Document)
                .Include(log => log.ActorUser)
                .OrderByDescending(log => log.OccurredAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> UpdateScanStatusAsync(int documentId, string scanStatus)
        {
            var doc = await _db.Documents.FindAsync(documentId);
            if (doc == null) return false;

            doc.ScanStatus = scanStatus;
            await _db.SaveChangesAsync();
            return true;
        }

        private IQueryable<Document> ApplyFilter(IQueryable<Document> query, DocumentFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.ToLower();
                query = query.Where(d =>
                    d.Title.ToLower().Contains(term) ||
                    (d.Description != null && d.Description.ToLower().Contains(term)) ||
                    (d.Tags != null && d.Tags.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(filter.Category))
                query = query.Where(d => d.Category == filter.Category);

            if (filter.ProjectId.HasValue)
                query = query.Where(d => d.ProjectId == filter.ProjectId.Value);

            if (!string.IsNullOrWhiteSpace(filter.FileType))
                query = query.Where(d => d.FileType.Contains(filter.FileType));

            if (filter.UploadedAfter.HasValue)
                query = query.Where(d => d.UploadedAt >= filter.UploadedAfter.Value);

            if (filter.UploadedBefore.HasValue)
                query = query.Where(d => d.UploadedAt <= filter.UploadedBefore.Value);

            return query;
        }

        private Task LogActivityAsync(int documentId, int actorUserId, string action, string? details = null)
        {
            _db.DocumentActivityLogs.Add(new DocumentActivityLog
            {
                DocumentId = documentId,
                ActorUserId = actorUserId,
                Action = action,
                OccurredAt = DateTime.UtcNow,
                Details = details
            });
            return Task.CompletedTask;
        }
    }
}
