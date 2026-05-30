namespace BimExplorer.Data.Models;

public class IndexedFolder
{
    public int Id { get; set; }
    public required string Path { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastIndexedAtUtc { get; set; }
}
