using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models
{
    public class TaskDocument
    {
        [Key]
        public int TaskDocumentId { get; set; }

        public int TaskId { get; set; }

        public int DocumentId { get; set; }

        public DateTime AttachedAt { get; set; } = DateTime.UtcNow;

        public int AttachedByUserId { get; set; }

        // Navigation properties
        [ForeignKey(nameof(TaskId))]
        public TaskItem Task { get; set; } = null!;

        [ForeignKey(nameof(DocumentId))]
        public Document Document { get; set; } = null!;

        [ForeignKey(nameof(AttachedByUserId))]
        public User AttachedByUser { get; set; } = null!;
    }
}
