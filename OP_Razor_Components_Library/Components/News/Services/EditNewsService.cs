using Microsoft.AspNetCore.Components;
using OP_Shared_Library.Services;

namespace OP_Razor_Components_Library.Components.News.Services;

public class EditNewsService
{
    #region Dependency Injection
    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;
    private readonly IYamlService _yamlService;
    private readonly IMinIoService _minIoService;
    private readonly ICustomLogger _customLogger;
    #endregion

    #region Private Properties
    private const string FileNameCs = "news.yaml";
    private const string FileNameEn = "news_en.yaml";
    private string _currentLanguage = "cs";
    public const string MinIoFolder = "news";
    #endregion

    #region Public properties
    public List<NewsItem> Items { get; private set; } = new();
    #endregion

    #region Ctor
    public EditNewsService(HttpClient http,
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
    #endregion

    #region Public Methods
    public async Task LoadAsync(string language)
    {
        _currentLanguage = NormalizeLanguage(language);

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

    public void AddNewItem()
    {
        Items.Add(new NewsItem
        {
            Id = Guid.NewGuid(),
            Title = "",
            Text1 = "",
            Text2 = "",
            ImageUrl = "",
            ImageSignedUrl = ""
        });
    }

    public void RemoveItem(NewsItem item)
    {
        Items.Remove(item);
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
            _customLogger.MyLogger.Error($"EditNewsService: ResolveImageUrlAsync: Unable to build public URL for '{imageReference}': {ex.Message}");
            return normalizedImageReference;
        }
    }
    #endregion

    #region Private Methods
    private string NormalizeLanguage(string language)
    {
        return string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "cs";
    }

    private async Task<List<NewsItem>> LoadYamlAsync(string fileName)
    {
        var yamlPath = _yamlService.EnsureYamlPath(fileName);

        try
        {
            if (File.Exists(yamlPath))
            {
                await using var fileStream = File.OpenRead(yamlPath);
                return _yamlService.LoadYamlList<NewsItem>(yamlPath, fileStream) ?? new();
            }
        }
        catch
        {
        }

        return new();
    }

    private List<NewsItem> MergeSharedAndLocalized(List<NewsItem> sharedItems, List<NewsItem> localizedItems)
    {
        var result = new List<NewsItem>();

        for (var i = 0; i < sharedItems.Count; i++)
        {
            var sharedItem = sharedItems[i];
            var localizedItem = FindMatchingItem(localizedItems, sharedItem.Id, i);

            result.Add(new NewsItem
            {
                Id = sharedItem.Id,
                Title = localizedItem?.Title ?? sharedItem.Title,
                Text1 = localizedItem?.Text1 ?? sharedItem.Text1,
                Text2 = localizedItem?.Text2 ?? sharedItem.Text2,
                ImageUrl = sharedItem.ImageUrl,
                ImageSignedUrl = sharedItem.ImageSignedUrl
            });
        }

        return result;
    }

    private List<NewsItem> ProjectSharedItems(List<NewsItem> currentItems, List<NewsItem> existingSharedItems)
    {
        var result = new List<NewsItem>();

        for (var i = 0; i < currentItems.Count; i++)
        {
            var currentItem = currentItems[i];
            var existingSharedItem = FindMatchingItem(existingSharedItems, currentItem.Id, i);

            result.Add(new NewsItem
            {
                Id = currentItem.Id,
                Title = existingSharedItem?.Title ?? currentItem.Title,
                Text1 = existingSharedItem?.Text1 ?? currentItem.Text1,
                Text2 = existingSharedItem?.Text2 ?? currentItem.Text2,
                ImageUrl = currentItem.ImageUrl,
                ImageSignedUrl = currentItem.ImageSignedUrl
            });
        }

        return result;
    }

    private List<NewsItem> ProjectLocalizedItems(List<NewsItem> currentItems, List<NewsItem> existingLocalizedItems)
    {
        var result = new List<NewsItem>();

        for (var i = 0; i < currentItems.Count; i++)
        {
            var currentItem = currentItems[i];
            var existingLocalizedItem = FindMatchingItem(existingLocalizedItems, currentItem.Id, i);

            result.Add(new NewsItem
            {
                Id = currentItem.Id,
                Title = currentItem.Title,
                Text1 = currentItem.Text1,
                Text2 = currentItem.Text2,
                ImageUrl = currentItem.ImageUrl,
                ImageSignedUrl = existingLocalizedItem?.ImageSignedUrl ?? currentItem.ImageSignedUrl
            });
        }

        return result;
    }

    private List<NewsItem> CloneItems(List<NewsItem> items)
    {
        return items.Select(item => new NewsItem
        {
            Id = item.Id,
            Title = item.Title,
            Text1 = item.Text1,
            Text2 = item.Text2,
            ImageUrl = item.ImageUrl,
            ImageSignedUrl = item.ImageSignedUrl
        }).ToList();
    }

    private NewsItem? FindMatchingItem(List<NewsItem> items, Guid id, int index)
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

    private async Task RefreshSignedUrlsAsync()
    {
        foreach (var item in Items)
        {
            item.ImageSignedUrl = await ResolveImageUrlAsync(item.ImageUrl);
        }
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
    #endregion
}
