using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models
{
    public class DocumentActivityLog
    {
        [Key]
        public int ActivityLogId { get; set; }

        public int DocumentId { get; set; }

        public int ActorUserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Details { get; set; }

        // Navigation properties
        [ForeignKey(nameof(DocumentId))]
        public Document Document { get; set; } = null!;

        [ForeignKey(nameof(ActorUserId))]
        public User ActorUser { get; set; } = null!;
    }
}
