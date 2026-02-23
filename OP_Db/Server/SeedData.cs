using OP_Db.Server.Entities;

namespace OP_Db.Server;
public static class SeedData
{
    public static void Initialize(OP_Db_Context db)
    {
        // Create array of Whatever table
        var whatevers = new OP_Item_Entity[]
        {
        };

        // Add created array to created db place
        db.OP_Items.AddRange(whatevers);
        db.SaveChanges();
    }
}
