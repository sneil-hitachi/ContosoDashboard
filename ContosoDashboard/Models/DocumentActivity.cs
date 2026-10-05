using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentActivity
{
    [Key]
    public int DocumentActivityId { get; set; }

    public int? DocumentId { get; set; }

    [Required]
    [MaxLength(255)]
    public string DocumentTitleSnapshot { get; set; } = string.Empty;

    public int? ActorUserId { get; set; }

    [Required]
    [MaxLength(255)]
    public string ActorNameSnapshot { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }
}