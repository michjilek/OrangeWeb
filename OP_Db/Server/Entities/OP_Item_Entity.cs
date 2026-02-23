
namespace OP_Db.Server.Entities;

public class OP_Item_Entity : EntityBase
{
    public string? Name { get; set; }
    public string? Ip { get; set; }
    public string? Note { get; set; }
    public int Sync { get; set; }
}
