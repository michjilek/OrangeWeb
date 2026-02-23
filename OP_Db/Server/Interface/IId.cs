using System.ComponentModel.DataAnnotations.Schema;

namespace OP_Db.Server.Interface;
public interface IId
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    Guid Id { get; set; }
}
