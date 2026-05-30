namespace BimExplorer.Data.Models;

public class BimFileTag
{
    public int BimFileId { get; set; }
    public BimFile BimFile { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
