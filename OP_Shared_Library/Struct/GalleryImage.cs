using YamlDotNet.Serialization;

namespace OP_Shared_Library.Struct;
public class GalleryImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // Object key of the image stored in MinIO
    public string ImagePath { get; set; } = string.Empty;
    public string ImageTitle { get; set; } = string.Empty;
    // Runtime-only signed URL for displaying the image.
    [YamlIgnore]
    public string ImageSignedUrl { get; set; }
}
