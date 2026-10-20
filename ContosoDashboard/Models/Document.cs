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
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string FileType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public int UploaderUserId { get; set; }

    public int? ProjectId { get; set; }

    public int? TaskId { get; set; }

    public virtual User Uploader { get; set; } = null!;

    public virtual Project? Project { get; set; }

    public virtual TaskItem? Task { get; set; }

    public virtual ICollection<DocumentTag> Tags { get; set; } = new List<DocumentTag>();
}