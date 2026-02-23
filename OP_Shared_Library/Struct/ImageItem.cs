namespace OP_Shared_Library.Struct;
public class ImageItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ImageUrl { get; set; } = string.Empty;
    public string Alt { get; set; }
}

// For YAML
public class ImagesFile
{
    public List<ImageItem> Images { get; set; } = new();
}
