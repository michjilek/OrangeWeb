using OP_Db.Server.Entities.Interface;
using System.ComponentModel.DataAnnotations.Schema;

namespace OP_Db.Server.Entities;
public class EntityBase : IEntity
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
}
