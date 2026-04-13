using Microsoft.AspNetCore.Components;
using OP_Shared_Library.Services;

namespace OP_Razor_Components_Library.Components.Actualities.Services;

public class EditActualitiesService
{
    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;
    private readonly IYamlService _yamlService;
    private readonly IMinIoService _minIoService;
    private readonly ICustomLogger _customLogger;

    private const string FileNameCs = "actualities.yaml";
    private const string FileNameEn = "actualities_en.yaml";
    private string _currentLanguage = "cs";

    public const string MinIoFolder = "actualities";

    public List<ActualityItem> Items { get; private set; } = new();

    public EditActualitiesService(HttpClient http,
                                  NavigationManager navigation,
                                  IYamlService yamlService,
                                  IMinIoService minIoService,
                                  ICustomLogger customLogger)
    {
        _http = http;
        _navigation = navigation;
        _yamlService = yamlService;
        _minIoService = minIoService;
        _customLogger = customLogger;
    }

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
        catch
        {
            Items = new();
        }

        await RefreshSignedUrlsAsync();
    }

    public async Task SaveAsync(string webRootPath)
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

    public void RemoveItem(ActualityItem item)
    {
        Items.Remove(item);
    }

    private static string NormalizeLanguage(string language)
    {
        return string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "cs";
    }

    public async Task<string> ResolveImageUrlAsync(string imageReference)
    {
        var normalizedImageReference = _minIoService.NormalizeImageReference(imageReference);

        try
        {
            return await _minIoService.GetPublicUrl(normalizedImageReference);
        }
        catch (Exception ex)
        {
            _customLogger.MyLogger.Error($"EditActualitiesService: ResolveImageUrlAsync: Unable to build public URL for '{imageReference}': {ex.Message}");
            return normalizedImageReference;
        }
    }

    private async Task RefreshSignedUrlsAsync()
    {
        foreach (var item in Items)
        {
            item.ImageSignedUrl = await ResolveImageUrlAsync(item.ImageUrl);
        }
    }

    private async Task<List<ActualityItem>> LoadYamlAsync(string fileName)
    {
        var yamlPath = _yamlService.EnsureYamlPath(fileName);
        if (!File.Exists(yamlPath))
        {
            return new();
        }

        await using var fileStream = File.OpenRead(yamlPath);
        return _yamlService.LoadYamlList<ActualityItem>(yamlPath, fileStream) ?? new();
    }

    private static List<ActualityItem> CloneItems(List<ActualityItem> items)
    {
        return items.Select(item => new ActualityItem
        {
            Id = item.Id,
            CreatedAt = item.CreatedAt,
            Text = item.Text,
            ImageUrl = item.ImageUrl,
            ImageSignedUrl = item.ImageSignedUrl
        }).ToList();
    }

    private static ActualityItem? FindMatchingItem(List<ActualityItem> items, Guid id, int index)
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

    private static List<ActualityItem> MergeSharedAndLocalized(List<ActualityItem> sharedItems, List<ActualityItem> localizedItems)
    {
        var result = new List<ActualityItem>();

        for (var i = 0; i < sharedItems.Count; i++)
        {
            var sharedItem = sharedItems[i];
            var localizedItem = FindMatchingItem(localizedItems, sharedItem.Id, i);

            result.Add(new ActualityItem
            {
                Id = sharedItem.Id,
                CreatedAt = localizedItem?.CreatedAt ?? sharedItem.CreatedAt,
                Text = localizedItem?.Text ?? sharedItem.Text,
                ImageUrl = sharedItem.ImageUrl,
                ImageSignedUrl = sharedItem.ImageSignedUrl
            });
        }

        return result;
    }

    private static List<ActualityItem> ProjectSharedItems(List<ActualityItem> currentItems, List<ActualityItem> existingSharedItems)
    {
        var result = new List<ActualityItem>();

        for (var i = 0; i < currentItems.Count; i++)
        {
            var currentItem = currentItems[i];
            var existingSharedItem = FindMatchingItem(existingSharedItems, currentItem.Id, i);

            result.Add(new ActualityItem
            {
                Id = currentItem.Id,
                CreatedAt = existingSharedItem?.CreatedAt ?? currentItem.CreatedAt,
                Text = existingSharedItem?.Text ?? currentItem.Text,
                ImageUrl = currentItem.ImageUrl,
                ImageSignedUrl = currentItem.ImageSignedUrl
            });
        }

        return result;
    }

    private static List<ActualityItem> ProjectLocalizedItems(List<ActualityItem> currentItems, List<ActualityItem> existingLocalizedItems)
    {
        var result = new List<ActualityItem>();

        for (var i = 0; i < currentItems.Count; i++)
        {
            var currentItem = currentItems[i];
            var existingLocalizedItem = FindMatchingItem(existingLocalizedItems, currentItem.Id, i);

            result.Add(new ActualityItem
            {
                Id = currentItem.Id,
                CreatedAt = currentItem.CreatedAt,
                Text = currentItem.Text,
                ImageUrl = currentItem.ImageUrl,
                ImageSignedUrl = existingLocalizedItem?.ImageSignedUrl ?? currentItem.ImageSignedUrl
            });
        }

        return result;
    }

    private async Task CleanUnusedImagesAsync()
    {
        var prefix = $"{MinIoFolder.Trim('/')}/";

        var sharedItems = await LoadYamlAsync(FileNameCs);
        var localizedItems = await LoadYamlAsync(FileNameEn);

        var referenced = sharedItems
            .Concat(localizedItems)
            .Select(item => _minIoService.NormalizeImageReference(item.ImageUrl))
            .Where(imageUrl => !string.IsNullOrWhiteSpace(imageUrl)
                && imageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referencedWithVariants = BuildReferencedSetWithVariants(referenced);
        await _minIoService.RemoveObjectInBucket(prefix, referencedWithVariants);
    }

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
