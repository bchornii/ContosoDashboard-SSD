using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models
{
    public class DocumentShare
    {
        [Key]
        public int DocumentShareId { get; set; }

        public int DocumentId { get; set; }

        public int SharedWithUserId { get; set; }

        public int SharedByUserId { get; set; }

        public DateTime SharedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(DocumentId))]
        public Document Document { get; set; } = null!;

        [ForeignKey(nameof(SharedWithUserId))]
        public User SharedWithUser { get; set; } = null!;

        [ForeignKey(nameof(SharedByUserId))]
        public User SharedByUser { get; set; } = null!;
    }
}
