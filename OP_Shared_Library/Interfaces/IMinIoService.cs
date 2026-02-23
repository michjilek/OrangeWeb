using Microsoft.AspNetCore.Components.Forms;
using Minio;

public interface IMinIoService
{
    IMinioClient MinioClient { get; }
    HashSet<string> AllowedImageExtensions { get; }

    Task EnsureBucketAsync();
    Task<string> GetPublicUrl(string objectKey);
    string NormalizeImageReference(string imageReference);
    Task PutObjectAsync(string objectName, MemoryStream ms, IBrowserFile file);
    Task RemoveObjectInBucket(string prefix, HashSet<string> referenced);
}
