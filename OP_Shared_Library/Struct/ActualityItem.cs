using YamlDotNet.Serialization;

public class ActualityItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Text { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    [YamlIgnore]
    public string ImageSignedUrl { get; set; }
}
