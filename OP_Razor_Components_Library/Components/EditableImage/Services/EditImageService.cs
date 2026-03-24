using Microsoft.AspNetCore.Components.Forms;
using OP_Shared_Library.Struct;
using OP_Shared_Library.Services;
using System.Linq;

namespace OP_Razor_Components_Library.Components.EditableImage.Services;

public class EditImageService
{
    #region Dependency Injection
    private readonly ICustomLogger _logger;
    private readonly IMinIoService _minIoService;
    private readonly IYamlService _yamlService;
    #endregion

    #region Private Properties
    // YAML filenames
    private const string _fileName_orig = "images.yaml";
    private const string _fileName_en = "images_en.yaml";
    private string _fileName = _fileName_orig;
    private string _yamlPath = string.Empty;
    public const string MinIoFolder = "gallery";

    // Mapping: key -> objectKey (NOTE: not a URL!)
    private List<ImageMap> _map = [];
    #endregion

    #region Ctor
    public EditImageService(ICustomLogger logger,
                            IMinIoService minIoService,
                            IYamlService yamlService)
    {
        _logger = logger;
        _minIoService = minIoService;
        _yamlService = yamlService;
    }
    #endregion

    #region Public Methods
    // Switch language file ("cs" => images.yaml, otherwise images_en.yaml)
    public void ChangeLangFile(string language)
    {
        _fileName = language?.ToLowerInvariant() == "cs" ? _fileName_orig : _fileName_en;
    }
    // Load mapping from yaml
    public async Task LoadAsync(string language)
    {
        // Change lang file
        ChangeLangFile(language);

        // Use changed lang file to ensure yaml path
        _yamlPath = _yamlService.EnsureYamlPath(_fileName);

        // If path doesnt exists, create new map dictionary <string,string>
        if (!File.Exists(_yamlPath))
        {
            _map = [];
            return;
        }

        var yaml = await _yamlService.ReadYamlFile(_yamlPath);

        _map = string.IsNullOrEmpty(yaml)
            ? new()
            : _yamlService.Deserializer.Deserialize<List<ImageMap>>(yaml) ?? new();
    }
    public string GetObjectKey(string logicalKey)
    {
        if (string.IsNullOrWhiteSpace(logicalKey)) return null;

        var v = _map.Find(x => string.Equals(x.ImageTag, logicalKey, StringComparison.OrdinalIgnoreCase));

        if (v == null) return null;

        return string.IsNullOrWhiteSpace(v.ImageUrl) ? null : v.ImageUrl;
    }
    public void Set(string logicalKey, string objectKey)
    {
        if (_map is null /*|| !_map.Any()*/) return;

        var v = _map.Find(x => string.Equals(x.ImageTag, logicalKey, StringComparison.OrdinalIgnoreCase));

        if (v != null)
        {
            v.ImageUrl = objectKey;
        }
        else
        {
            _map.Add(new ImageMap
            {
                ImageTag = logicalKey,
                ImageUrl = objectKey
            });
        }
    }
    public void Remove(string logicalKey)
    {
        if (_map is null || string.IsNullOrWhiteSpace(logicalKey))
        {
            return;
        }

        _map.RemoveAll(x => string.Equals(x.ImageTag, logicalKey, StringComparison.OrdinalIgnoreCase));
    }
    // Persist YAML to disk
    public async Task SaveToYamlAsync()
    {
        await _yamlService.SaveToYamlAsync(_map, _fileName);
    }
    // Create a public URL for a given logical key
    public async Task<string> GetSignedUrlAsync(string logicalKey)
    {
        var objectKey = GetObjectKey(logicalKey) ?? _logger.LogAndReturn($"No mapping for '{logicalKey}'.", error: false);

        var result = await _minIoService.GetPublicUrl(objectKey);

        return result;
    }
    // Upload image → returns objectKey
    public async Task<string> SaveImageAsync(IBrowserFile file, string relativeFolder = "", long maxBytes = 10_000_000)
    {
        if (file is null) { _logger.LogAndReturn($"File is null : {nameof(file)}"); }

        var ext = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(ext) || !_minIoService.AllowedImageExtensions.Contains(ext))
        {
            _logger.LogAndReturn($"File type '{ext}' is not allowed.");
        }

        await _minIoService.EnsureBucketAsync();

        //var objectName = BuildObjectName(relativeFolder, ext);
        // Use ResponsiveImageHelper to build the object key
        var objectName = ResponsiveImageHelper.BuildResponsiveObjectKey(relativeFolder, ext);

        // Upload responsive variants
        await UploadResponsiveVariantsAsync(file, objectName, maxBytes);

        return objectName; // we store only the base object key
    }
    // Upload + write to YAML + optionally clean up unused files in the given relativeFolder
    public async Task<string> SaveImageAndMapAsync(string key, IBrowserFile file, long maxBytes = 10_000_000, bool includeOtherLanguageMaps = true)
    {
        var objectKey = await SaveImageAsync(file, MinIoFolder, maxBytes);
        Set(key, objectKey);
        await SaveToYamlAsync();

        await CleanUnusedImagesAsync(MinIoFolder, includeOtherLanguageMaps);
        return objectKey;
    }
    public async Task DeleteImageAndMapAsync(string key, bool includeOtherLanguageMaps = true)
    {
        Remove(key);
        await SaveToYamlAsync();
        await CleanUnusedImagesAsync(MinIoFolder, includeOtherLanguageMaps);
    }
    // Delete objects under the relativeFolder that are not referenced in YAML files
    public async Task CleanUnusedImagesAsync(string relativeFolder = "", bool includeOtherLanguageMaps = true)
    {
        if (string.IsNullOrWhiteSpace(relativeFolder))
        {
            return; // safety brake – never delete across the entire bucket
        }

        var prefix = relativeFolder.Trim('/') + "/";

        var referenced = _map
                            .Select(v => v.ImageUrl)
                            .Where(u => !string.IsNullOrWhiteSpace(u)
                       && u!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (includeOtherLanguageMaps)
        {
            var otherFileName = _fileName == _fileName_orig ? _fileName_en : _fileName_orig;
            var otherPath = _yamlService.EnsureYamlPath(otherFileName);
            if (File.Exists(otherPath))
            {
                try
                {
                    var yaml = await File.ReadAllTextAsync(otherPath);
                    var other = string.IsNullOrWhiteSpace(yaml)
                        ? new List<ImageMap>()
                        : _yamlService.Deserializer.Deserialize<List<ImageMap>>(yaml) ?? new();
                    foreach (var v in other)
                    {
                        if (!string.IsNullOrWhiteSpace(v.ImageUrl) && v.ImageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            referenced.Add(v.ImageUrl);
                        }
                    }
                }
                catch
                {
                    _logger.MyLogger.Error($"EditImageService: CleanUnusedImageAsync: Issuse in 'if (File.Exists(otherPath))' block.");
                }
            }
        }

        // For responsive images, we need to consider variants as well
        var referencedWithVariants = new HashSet<string>(referenced, StringComparer.OrdinalIgnoreCase);

        // Add variants
        foreach (var key in referenced)
        {
            // Skip non-responsive keys
            if (!ResponsiveImageHelper.IsResponsiveKey(key))
            {
                continue;
            }

            // Add all variants for this key
            var maxWidth = ResponsiveImageHelper.VariantWidths.Max();

            // Base key is the max width
            foreach (var width in ResponsiveImageHelper.VariantWidths)
            {
                // Build variant key
                // For max width, use the base key
                var variantKey = width == maxWidth ? key : ResponsiveImageHelper.BuildVariantKey(key, width);

                // Add to referenced set
                referencedWithVariants.Add(variantKey);
            }
        }

        // Remove unreferenced variants
        await _minIoService.RemoveObjectInBucket(prefix, referencedWithVariants);
    }
    #endregion

    #region Private Methods
    private async Task UploadResponsiveVariantsAsync(IBrowserFile file, string objectKey, long maxBytes)
    {
        // Gte max width
        var maxWidth = ResponsiveImageHelper.VariantWidths.Max();

        // Upload each variant
        foreach (var width in ResponsiveImageHelper.VariantWidths)
        {
            // Resize image by current width variant
            var resized = await file.RequestImageFileAsync(format: file.ContentType, maxWidth: width, maxHeight: ResponsiveImageHelper.MaxUploadedHeight);

            // Create stream for MinIo, copy because of original stream is readonly and we need to reset position
            await using var readStream = resized.OpenReadStream(maxBytes); // open read stream
            using var ms = new MemoryStream(); // we need stream for ImageSharp
            await readStream.CopyToAsync(ms); // copy to memory stream
            ms.Position = 0; // reset position

            // Determine object key for this variant
            var key = width == maxWidth ? objectKey : ResponsiveImageHelper.BuildVariantKey(objectKey, width);

            // Upload to MinIO
            await _minIoService.PutObjectAsync(key, ms, resized);
        }
    }
    #endregion
}
