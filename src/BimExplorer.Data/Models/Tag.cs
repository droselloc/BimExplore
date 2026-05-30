namespace BimExplorer.Data.Models;

public class Tag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Category { get; set; }

    // Navigation
    public ICollection<BimFileTag> BimFileTags { get; set; } = [];
}
