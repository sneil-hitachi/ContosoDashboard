using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentTag
{
    [Key]
    public int DocumentTagId { get; set; }

    public int DocumentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Value { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string NormalizedValue { get; set; } = string.Empty;

    public virtual Document Document { get; set; } = null!;
}