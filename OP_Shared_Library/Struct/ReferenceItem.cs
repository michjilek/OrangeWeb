using YamlDotNet.Serialization;

public class ReferenceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Author { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    [YamlIgnore]
    public string ImageSignedUrl { get; set; }
}
