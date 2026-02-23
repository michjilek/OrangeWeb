using Microsoft.AspNetCore.Components.Forms;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Op_LP.Services;

public class MinIoService : IMinIoService
{
    #region Dependency Injection
    // MinIoClient ensure communication with MinIo object storage
    private IMinioClient _minioClient;
    private IConfiguration _config;
    private ICustomLogger _logger;
    #endregion

    #region Private Properties
    private string? _bucket;
    private string? _publicBaseUrl;
    private readonly HashSet<string> _allowedImageExtensions =
    new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
    #endregion

    #region Public Properties
    public IMinioClient MinioClient => _minioClient;
    public HashSet<string> AllowedImageExtensions => _allowedImageExtensions;
    #endregion

    #region Ctor
    public MinIoService(IMinioClient minioClient,
                        IConfiguration config,
                        ICustomLogger logger
                        )
    {
        _minioClient = minioClient;
        _config = config;
        _logger = logger;

        EnsureConfig();
    }
    #endregion

    #region Private Methods
    private void EnsureConfig()
    {
        // Get bucket name from appsettings.json
        _bucket = _config["Minio:Bucket"] ?? "default_project_name";
        // Get publicbaseurl, url clients will download images from
        _publicBaseUrl = (_config["Minio:PublicBaseUrl"] ?? _logger.LogAndReturn("Minio:PublicBaseUrl missing."));

        _logger.TestLog($"MinIo: EnsureConfig: _bucket <- {_bucket}, _publicBaseUrl <- {_publicBaseUrl}");
    }
    private string BuildPublicObjectUrl(string objectKey)
    {
        var normalized = NormalizeImageReference(objectKey);
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

        // passthrough
        if (normalized.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            return normalized;

        if (string.IsNullOrWhiteSpace(_publicBaseUrl))
        {
            _logger.MyLogger.Error("Minio: PublicBaseUrl is empty.");
            return normalized;
        }

        var baseUrl = _publicBaseUrl.TrimEnd('/');
        var key = normalized.TrimStart('/'); // e.g. "gallery/xxxx.webp"

        return $"{baseUrl}/{key}";
    }

    // Helper for catching and logging MinIO exceptions
    private async Task SafeMinioCall(Func<Task> action, string context)
    {
        try
        {
            await action();
        }
        catch (MinioException ex)
        {
            _logger.MyLogger.Error($"MinIO Error in {context}: {ex.Message}");
            _logger.MyLogger.Debug(ex.ToString());
            throw;
        }
        catch (Exception ex)
        {
            _logger.MyLogger.Error($"Unexpected error in {context}: {ex.Message}");
            _logger.MyLogger.Debug(ex.ToString());
            throw;
        }
    }
    private async Task<T> SafeMinioCall<T>(Func<Task<T>> action, string context)
    {
        try
        {
            return await action();
        }
        catch (MinioException ex)
        {
            _logger.MyLogger.Error($"MinIO Error in {context}: {ex.Message}");
            _logger.MyLogger.Debug(ex.ToString());
            throw;
        }
        catch (Exception ex)
        {
            _logger.MyLogger.Error($"Unexpected error in {context}: {ex.Message}");
            _logger.MyLogger.Debug(ex.ToString());
            throw;
        }
    }
    #endregion

    #region Public Methods
    // Creates object in bucket by coming object name
    public async Task PutObjectAsync(string objectName, MemoryStream ms, IBrowserFile file)
    {
        await SafeMinioCall(async () =>
        {
            await _minioClient.PutObjectAsync(new PutObjectArgs()
               .WithBucket(_bucket)
               .WithObject(objectName)
               .WithStreamData(ms)
               .WithObjectSize(ms.Length)
               .WithContentType(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType)
               .WithHeaders(new Dictionary<string, string>
               {
                   ["Cache-Control"] = "public, max-age=31536000, immutable"
               })
           );

            _logger.TestLog($"MinIo: Uploaded object '{objectName}' ({ms.Length} bytes).");
        }, nameof(PutObjectAsync));
    }
    // Return a public URL for a given object key
    // Bucket is public download, so we can skip presigning for faster image loads.
    public async Task<string> GetPublicUrl(string objectKey)
    {
        return await Task.FromResult(BuildPublicObjectUrl(objectKey));
    }
    // Remove object from bucket
    public async Task RemoveObjectInBucket(string prefix, HashSet<string> referenced)
    {
        await SafeMinioCall(async () =>
        {
            // Get objects to remove
            var objs = _minioClient.ListObjectsEnumAsync(new ListObjectsArgs()
                .WithBucket(_bucket)
                .WithPrefix(prefix)
                .WithRecursive(true));

            // Foreach with key, remove
            await foreach (var o in objs)
            {
                if (!referenced.Contains(o.Key))
                {
                    try
                    {
                        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                            .WithBucket(_bucket)
                            .WithObject(o.Key));

                        _logger.TestLog($"MinIo: Removed '{o.Key}' from '{_bucket}' bucket.");
                    }
                    catch (MinioException ex)
                    {
                        _logger.MyLogger.Error($"MinIo: Cannot remove '{o.Key}' from '{_bucket}': {ex.Message}");
                    }
                }
            }
        }, nameof(RemoveObjectInBucket));
    }
    // Ensure the bucket exists
    public async Task EnsureBucketAsync()
    {
        await SafeMinioCall(async () =>
            {
                var exists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket));

                if (!exists)
                {
                    // If doesn't exist, create bucket
                    await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket));
                    _logger.TestLog($"MinIo: Bucket '{_bucket}' created.");
                }
                else
                {
                    _logger.TestLog($"MinIo: Bucket '{_bucket}' already exists.");
                }
            }, nameof(EnsureBucketAsync));
    }
    // Get Normalized Image Reference
    public string NormalizeImageReference( string imageReference)
    {
        // Check if is null or empty
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            return string.Empty;
        }

        // Remove white spacing...
        var reference = imageReference.Trim();

        // If reference was already normalized
        if (reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
         || reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
         || reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
         || imageReference.StartsWith("/"))
        {
            return reference;
        }

        // Remove all / from reference at his start
        reference = reference.TrimStart('/');

        // Return reference
        return reference;
    }
    #endregion
}
