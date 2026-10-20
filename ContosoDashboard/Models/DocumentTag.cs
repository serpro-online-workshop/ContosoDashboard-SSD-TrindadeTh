using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentTag
{
    [Key]
    public int DocumentTagId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Tag { get; set; } = string.Empty;

    public virtual Document Document { get; set; } = null!;
}