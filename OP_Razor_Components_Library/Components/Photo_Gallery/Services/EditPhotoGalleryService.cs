using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;
using OP_Shared_Library.Services;
using OP_Shared_Library.Struct;

namespace OP_Razor_Components_Library.Components.Photo_Gallery.Services;
public class EditPhotoGalleryService
{
    #region Dependency Injection
    private readonly NavigationManager _navigation;
    private readonly HttpClient _httpClient;
    private readonly IHostEnvironment _env;
    private readonly IYamlService _yamlService;
    private readonly IMinIoService _minIoService;
    private readonly ICustomLogger _customLogger;
    #endregion

    #region Private Properties
    private const string FileNameCs = "photoGallery.yaml";
    private const string FileNameEn = "photoGallery_en.yaml";
    private string _currentLanguage = "cs";
    private string _filePath;
    private string _url;
    public const string MinIoFolder = "photo-gallery";
    #endregion

    #region Public Properties
    public List<GalleryImage> Items = new();
    #endregion

    #region Ctor
    public EditPhotoGalleryService(NavigationManager navigation,
                                   HttpClient http,
                                   IHostEnvironment env,
                                   IYamlService yamlService,
                                   IMinIoService minIoService,
                                   ICustomLogger customLogger
                                   )
    {
        _navigation = navigation;
        _httpClient = http;
        _env = env;
        _yamlService = yamlService;
        _minIoService = minIoService;
        _customLogger = customLogger;

        ReBuildPaths();
    }
    #endregion

    #region Private Methods
    private void ReBuildPaths()
    {
        _filePath = $"/data/{FileNameCs}";
        _url = $"{_navigation.BaseUri}{_filePath}";
    }
    private string NormalizeLanguage(string language)
    {
        return string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "cs";
    }
    private async Task<List<GalleryImage>> LoadYamlAsync(string fileName)
    {
        var yamlPath = _yamlService.EnsureYamlPath(fileName);

        if (!File.Exists(yamlPath))
        {
            return new();
        }

        await using var fileStream = File.OpenRead(yamlPath);
        return _yamlService.LoadYamlList<GalleryImage>(yamlPath, fileStream) ?? new();
    }
    private List<GalleryImage> CloneItems(List<GalleryImage> items)
    {
        return items.Select(item => new GalleryImage
        {
            Id = item.Id,
            ImagePath = item.ImagePath,
            ImageTitle = item.ImageTitle,
            ImageSignedUrl = item.ImageSignedUrl
        }).ToList();
    }
    private GalleryImage? FindMatchingItem(List<GalleryImage> items, Guid id, int index)
    {
        var byId = items.FirstOrDefault(item => item.Id == id);
        if (byId is not null)
        {
            return byId;
        }

        if (index >= 0 && index < items.Count)
        {
            return items[index];
        }

        return null;
    }
    private List<GalleryImage> MergeSharedAndLocalized(List<GalleryImage> sharedItems, List<GalleryImage> localizedItems)
    {
        var result = new List<GalleryImage>();

        for (var i = 0; i < sharedItems.Count; i++)
        {
            var sharedItem = sharedItems[i];
            var localizedItem = FindMatchingItem(localizedItems, sharedItem.Id, i);

            result.Add(new GalleryImage
            {
                Id = sharedItem.Id,
                ImagePath = sharedItem.ImagePath,
                ImageTitle = localizedItem?.ImageTitle ?? sharedItem.ImageTitle,
                ImageSignedUrl = sharedItem.ImageSignedUrl
            });
        }

        return result;
    }
    private List<GalleryImage> ProjectSharedItems(List<GalleryImage> currentItems, List<GalleryImage> existingSharedItems)
    {
        var result = new List<GalleryImage>();

        for (var i = 0; i < currentItems.Count; i++)
        {
            var currentItem = currentItems[i];
            var existingSharedItem = FindMatchingItem(existingSharedItems, currentItem.Id, i);

            result.Add(new GalleryImage
            {
                Id = currentItem.Id,
                ImagePath = currentItem.ImagePath,
                ImageTitle = existingSharedItem?.ImageTitle ?? currentItem.ImageTitle,
                ImageSignedUrl = currentItem.ImageSignedUrl
            });
        }

        return result;
    }
    private List<GalleryImage> ProjectLocalizedItems(List<GalleryImage> currentItems, List<GalleryImage> existingLocalizedItems)
    {
        var result = new List<GalleryImage>();

        for (var i = 0; i < currentItems.Count; i++)
        {
            var currentItem = currentItems[i];
            var existingLocalizedItem = FindMatchingItem(existingLocalizedItems, currentItem.Id, i);

            result.Add(new GalleryImage
            {
                Id = currentItem.Id,
                ImagePath = currentItem.ImagePath,
                ImageTitle = currentItem.ImageTitle,
                ImageSignedUrl = existingLocalizedItem?.ImageSignedUrl ?? currentItem.ImageSignedUrl
            });
        }

        return result;
    }
    private void NormalizeImageReferences()
    {
        if (Items is null || !Items.Any()) return;

        foreach (var item in Items)
        {
            item.ImagePath = _minIoService.NormalizeImageReference(item.ImagePath);
        }
    }
    private async Task RefreshSignedUrlsAsync()
    {
        foreach (var item in Items)
        {
            item.ImagePath = _minIoService.NormalizeImageReference(item.ImagePath);
            item.ImageSignedUrl = await ResolveImageUrlAsync(item.ImagePath);
        }
    }
    #endregion

    #region Public Methods
    public async Task LoadAsync(string language)
    {
        _currentLanguage = NormalizeLanguage(language);

        try
        {
            var sharedItems = await LoadYamlAsync(FileNameCs);
            if (_currentLanguage == "cs")
            {
                Items = CloneItems(sharedItems);
            }
            else
            {
                var localizedItems = await LoadYamlAsync(FileNameEn);
                Items = MergeSharedAndLocalized(sharedItems, localizedItems);
            }
        }
        catch (Exception ex)
        {
            _customLogger.MyLogger.Error($"EditPhotoGalleryService: LoadAsync: Unable to read '{_url}': {ex.Message}");
            Items = new();
        }

        NormalizeImageReferences();
        await RefreshSignedUrlsAsync();
    }
    public async Task SaveAsync()
    {
        var sharedItems = await LoadYamlAsync(FileNameCs);
        var localizedItems = await LoadYamlAsync(FileNameEn);

        if (_currentLanguage == "cs")
        {
            var itemsToSave = CloneItems(Items);
            await _yamlService.SaveToYamlAsync(itemsToSave, FileNameCs);

            var synchronizedLocalizedItems = ProjectLocalizedItems(itemsToSave, localizedItems);
            await _yamlService.SaveToYamlAsync(synchronizedLocalizedItems, FileNameEn);
        }
        else
        {
            var synchronizedSharedItems = ProjectSharedItems(Items, sharedItems);
            await _yamlService.SaveToYamlAsync(synchronizedSharedItems, FileNameCs);

            var localizedToSave = ProjectLocalizedItems(Items, localizedItems);
            await _yamlService.SaveToYamlAsync(localizedToSave, FileNameEn);
        }

        await CleanUnusedImagesAsync();
    }
    public void AddNewItem()
    {
        Items.Add(new GalleryImage
        {
            Id = Guid.NewGuid(),
            ImagePath = "",
            ImageTitle = ""
        });
    }
    public void RemoveItem(GalleryImage item)
    {
        Items.Remove(item);
    }
    public async Task<string> ResolveImageUrlAsync(string imageReference)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            return null;
        }

        var normalizedReference = _minIoService.NormalizeImageReference(imageReference);

        try
        {
            return await _minIoService.GetPublicUrl(normalizedReference);
        }
        catch (Exception ex)
        {
            _customLogger.MyLogger.Error($"EditPhotoGalleryService: ResolveImageUrlAsync: Unable to build public URL for '{normalizedReference}': {ex.Message}");
            return normalizedReference;
        }
    }
    public string NormalizeImageReference(string imageReference)
    {
        return _minIoService.NormalizeImageReference(imageReference);
    }

    public async Task CleanUnusedImagesAsync()
    {
        var prefix = $"{MinIoFolder.Trim('/')}/";

        var sharedItems = await LoadYamlAsync(FileNameCs);
        var localizedItems = await LoadYamlAsync(FileNameEn);

        var referenced = sharedItems
            .Concat(localizedItems)
            .Select(item => _minIoService.NormalizeImageReference(item.ImagePath))
            .Where(imagePath => !string.IsNullOrWhiteSpace(imagePath)
                && imagePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referencedWithVariants = BuildReferencedSetWithVariants(referenced);
        await _minIoService.RemoveObjectInBucket(prefix, referencedWithVariants);
    }
    #endregion

    private static HashSet<string> BuildReferencedSetWithVariants(IEnumerable<string> referenced)
    {
        var result = new HashSet<string>(referenced, StringComparer.OrdinalIgnoreCase);
        var maxWidth = ResponsiveImageHelper.VariantWidths.Max();

        foreach (var key in result.ToArray())
        {
            if (!ResponsiveImageHelper.IsResponsiveKey(key))
            {
                continue;
            }

            foreach (var width in ResponsiveImageHelper.VariantWidths)
            {
                var variantKey = width == maxWidth
                    ? key
                    : ResponsiveImageHelper.BuildVariantKey(key, width);

                result.Add(variantKey);
            }
        }

        return result;
    }
}
