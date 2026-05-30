namespace BimExplorer.Data.Models;

public class BimFile
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public long FileSizeBytes { get; set; }
    public string? FileHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedUtc { get; set; }
    public DateTime IndexedAtUtc { get; set; } = DateTime.UtcNow;

    // BIM-specific metadata
    public string? ProjectName { get; set; }
    public string? Discipline { get; set; }
    public string? AuthorName { get; set; }
    public string? SoftwareVersion { get; set; }

    // Full-text searchable content extracted from the BIM file
    public string? ExtractedText { get; set; }

    // Preview
    public string? ThumbnailPath { get; set; }

    // Navigation
    public ICollection<BimFileTag> BimFileTags { get; set; } = [];
}
