using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentShare
{
    [Key]
    public int DocumentShareId { get; set; }

    public int DocumentId { get; set; }
    public int GrantedByUserId { get; set; }
    public int? RecipientUserId { get; set; }

    [MaxLength(100)]
    public string? RecipientDepartment { get; set; }

    public DateTime GrantedAtUtc { get; set; }

    public virtual Document Document { get; set; } = null!;
}