using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

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
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(400)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public long FileSize { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileType { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public int UploadedByUserId { get; set; }

    public int? ProjectId { get; set; }

    public virtual User Uploader { get; set; } = null!;
    public virtual Project? Project { get; set; }
    public virtual ICollection<DocumentTag> Tags { get; set; } = new List<DocumentTag>();
    public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
}