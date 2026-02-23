
namespace OP_Shared_Library.Services;

public static class ResponsiveImageHelper
{
    #region Public Static readonly
    public static readonly int[] VariantWidths = {320, 448, 480, 640, 768, 1024, 1366, 1600, 1920, 2560, 3840 };
    #endregion

    #region Public constants
    public const int MaxUploadedHeight = 4000;
    #endregion

    #region Public Static Methods
    // Builds a responsive object key given a relative folder and file extension
    public static string BuildResponsiveObjectKey(string relativeFolder, string ext)
    {
        // Trim any leading or trailing slashes from the relative folder
        var folder = string.IsNullOrWhiteSpace(relativeFolder) ? string.Empty : relativeFolder.Trim('/');

        // Normalize the extension to lowercase and ensure it starts with a dot
        var extension = string.IsNullOrWhiteSpace(ext) ? string.Empty : ext.ToLowerInvariant();

        // Ensure dot at start once again
        if (!extension.StartsWith('.'))
        {
            extension = $".{extension}";
        }

        // Generate a unique filename using a GUID -> _r like responsive
        var fileName = $"{Guid.NewGuid():N}_r{extension}";

        // Combine folder and filename
        return string.IsNullOrWhiteSpace(folder) ? fileName : $"{folder}/{fileName}";
    }
    // Determines if the given object key corresponds to a responsive image
    public static bool IsResponsiveKey(string objectKey)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return false;
        }

        // Extract the filename from the object key
        var fileName = Path.GetFileNameWithoutExtension(objectKey);

        // Check if the filename ends with _r (case-insensitive)
        return fileName.EndsWith("_r", StringComparison.OrdinalIgnoreCase);
    }
    // Builds a variant object key based on the base key and specified width
    public static string BuildVariantKey(string baseKey, int width)
    {
        // Get the file name without extension
        var directory = Path.GetDirectoryName(baseKey)?.Replace("\\", "/") ?? string.Empty;

        // Get the file name without extension
        var extension = Path.GetExtension(baseKey);

        // Get the file name without extension
        var fileName = Path.GetFileNameWithoutExtension(baseKey);

        // Build the variant name
        var variantName = $"{fileName}_w{width}{extension}";

        // Combine directory and variant name
        return string.IsNullOrWhiteSpace(directory) ? variantName : $"{directory.TrimEnd('/')}/{variantName}";
    }
    // Builds the srcset attribute value for a responsive image
    public static string BuildSrcSet(IMinIoService minIoService, string objectKey)
    {
        // Check for null or invalid inputs
        if (minIoService is null || string.IsNullOrWhiteSpace(objectKey) || !IsResponsiveKey(objectKey))
        {
            return null;
        }

        // Get the maximum width from the predefined variant widths
        var maxWidth = VariantWidths.Max();

        // Initialize a list to hold the srcset parts
        var parts = new List<string>();

        foreach (var width in VariantWidths)
        {
            // Determine the appropriate key for the current width
            // Dont need to build variant for max width as its the original image
            var key = width == maxWidth ? objectKey : BuildVariantKey(objectKey, width);

            // Get the public URL for the current key
            var url = minIoService.GetPublicUrl(key);

            // If a valid URL is returned, add it to the srcset parts
            if (!string.IsNullOrWhiteSpace(url.Result))
            {
                // Format for example: https://example.com/image_w480.jpg 480w
                parts.Add($"{url.Result} {width}w");
            }
        }

        // Format of 2 lokks like: https://example.com/image_w480.jpg 480w,
        //                         https://example.com/image_w768.jpg 768w, ...
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }
    #endregion
}
