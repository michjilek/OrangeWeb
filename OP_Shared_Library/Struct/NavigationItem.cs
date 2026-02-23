
namespace OP_Shared_Library.Struct;

public sealed class NavigationItem
{
    public Guid Id  { get; set; }
    public string Href { get; set; } = "/";
    public string TextId { get; set; } = "";
    public int Order { get; set; } = 0;
    public bool IsVisible { get; set; } = true;
}
