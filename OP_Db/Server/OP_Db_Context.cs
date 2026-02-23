using Microsoft.EntityFrameworkCore;
using OP_Db.Server.Entities;

namespace OP_Db.Server;
public class OP_Db_Context : DbContext
{
    #region Public Properties
    // Set place in db for array of whatever tables
    public DbSet<OP_Item_Entity> OP_Items { get; set; }
    #endregion

    #region Ctor
    public OP_Db_Context(DbContextOptions options) : base(options)
    {
    }
    #endregion
}
