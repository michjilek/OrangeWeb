using YamlDotNet.Serialization;

public class NewsItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Text1 { get; set; } = string.Empty;
    public string Text2 { get; set; } = string.Empty;
    // Object key of the image stored in MinIO
    public string ImageUrl { get; set; } = string.Empty;
    // Runtime-only pre-signed URL for displaying the image
    [YamlIgnore]
    public string ImageSignedUrl { get; set; }
}
