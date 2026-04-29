using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models
{
    public class Document
    {
        [Key]
        public int DocumentId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Tags { get; set; }

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string StoredFilePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FileType { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public int UploadedByUserId { get; set; }

        public int? ProjectId { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(50)]
        public string ScanStatus { get; set; } = "Pending";

        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        [ForeignKey(nameof(UploadedByUserId))]
        public User UploadedByUser { get; set; } = null!;

        [ForeignKey(nameof(ProjectId))]
        public Project? Project { get; set; }

        public ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
        public ICollection<DocumentActivityLog> ActivityLogs { get; set; } = new List<DocumentActivityLog>();
        public ICollection<TaskDocument> TaskDocuments { get; set; } = new List<TaskDocument>();
    }
}
