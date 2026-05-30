namespace BimExplorer.Data.Models;

public class Script
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Target { get; set; } = "Otro"; // Blender, Revit, Python, Otro...
    public string? Description { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
