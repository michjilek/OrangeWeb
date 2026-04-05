using Microsoft.AspNetCore.Components;
using OP_Razor_Components_Library.Components.ServiceList;

public class ServicesService
{
    #region Dependency Injection
    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;
    private readonly IYamlService _yamlService;
    private readonly ICustomLogger _customLogger;
    #endregion

    #region Private Properties
    private const string FileNameCs = "servicelist.yaml";
    private const string FileNameEn = "servicelist_en.yaml";
    private string _currentLanguage = "cs";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Task? _loadTask;
    #endregion

    #region Public Properties
    public List<ServiceListItem> Items { get; private set; } = new();
    #endregion

    #region Ctor
    public ServicesService(HttpClient http,
                           NavigationManager navigation,
                           IYamlService yamlService,
                           ICustomLogger customLogger)
    {
        _http = http;
        _navigation = navigation;
        _yamlService = yamlService;
        _customLogger = customLogger;
    }
    #endregion

    #region Public Methods
    public async Task LoadAsync(string language)
    {
        _currentLanguage = NormalizeLanguage(language);
        await LoadCoreAsync();
    }
    public async Task LoadCoreAsync()
    {
        // We need to use Lock because of possible concurrent calls
        await _lock.WaitAsync();

        try
        {
            try
            {
                // Primary source: resolved YAML path from YamlService
                // (external Configs/<brand>/Data when configured, else wwwroot/data).
                var sharedItems = await LoadYamlAsync(FileNameCs);
                EnsureItemKeys(sharedItems);

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
                _customLogger.MyLogger.Error($"TranslationService: LoadCoreAsync: {ex}");
            }
        }
        finally
        {
            _lock.Release();
            _loadTask = null;
        }
    }
    public async Task SaveAsync(string webRootPath)
    {
        EnsureItemKeys(Items);

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
    }
    public void AddNewItem()
    {
        Items.Add(new ServiceListItem
        {
            ImageKey = CreateImageKey(),
            Name = "",
            Description = "",
            Price = "",
            QrCodeKey = CreateQrCodeKey()
        });
    }
    public void RemoveItem(ServiceListItem item)
    {
        Items.Remove(item);
    }
    private static string NormalizeLanguage(string language)
    {
        return string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "cs";
    }

    private async Task<List<ServiceListItem>> LoadYamlAsync(string fileName)
    {
        var yamlPath = _yamlService.EnsureYamlPath(fileName);
        if (!File.Exists(yamlPath))
        {
            return new();
        }

        await using var fileStream = File.OpenRead(yamlPath);
        return _yamlService.LoadYamlList<ServiceListItem>(yamlPath, fileStream) ?? new();
    }

    private static List<ServiceListItem> CloneItems(List<ServiceListItem> items)
    {
        return items.Select(item => new ServiceListItem
        {
            ImageKey = item.ImageKey,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            QrCodeKey = item.QrCodeKey
        }).ToList();
    }

    private static ServiceListItem? FindMatchingItem(List<ServiceListItem> items, string imageKey, int index)
    {
        var byImageKey = !string.IsNullOrWhiteSpace(imageKey)
            ? items.FirstOrDefault(item => string.Equals(item.ImageKey, imageKey, StringComparison.Ordinal))
            : null;

        if (byImageKey is not null)
        {
            return byImageKey;
        }

        if (index >= 0 && index < items.Count)
        {
            return items[index];
        }

        return null;
    }

    private static List<ServiceListItem> MergeSharedAndLocalized(List<ServiceListItem> sharedItems, List<ServiceListItem> localizedItems)
    {
        var result = new List<ServiceListItem>();

        for (var i = 0; i < sharedItems.Count; i++)
        {
            var sharedItem = sharedItems[i];
            var localizedItem = FindMatchingItem(localizedItems, sharedItem.ImageKey, i);

            result.Add(new ServiceListItem
            {
                ImageKey = sharedItem.ImageKey,
                QrCodeKey = sharedItem.QrCodeKey,
                Name = localizedItem?.Name ?? sharedItem.Name,
                Description = localizedItem?.Description ?? sharedItem.Description,
                Price = localizedItem?.Price ?? sharedItem.Price
            });
        }

        return result;
    }

    private static List<ServiceListItem> ProjectSharedItems(List<ServiceListItem> currentItems, List<ServiceListItem> existingSharedItems)
    {
        var result = new List<ServiceListItem>();

        for (var i = 0; i < currentItems.Count; i++)
        {
            var currentItem = currentItems[i];
            var existingSharedItem = FindMatchingItem(existingSharedItems, currentItem.ImageKey, i);

            result.Add(new ServiceListItem
            {
                ImageKey = currentItem.ImageKey,
                QrCodeKey = currentItem.QrCodeKey,
                Name = existingSharedItem?.Name ?? currentItem.Name,
                Description = existingSharedItem?.Description ?? currentItem.Description,
                Price = existingSharedItem?.Price ?? currentItem.Price
            });
        }

        return result;
    }

    private static List<ServiceListItem> ProjectLocalizedItems(List<ServiceListItem> currentItems, List<ServiceListItem> existingLocalizedItems)
    {
        var result = new List<ServiceListItem>();

        for (var i = 0; i < currentItems.Count; i++)
        {
            var currentItem = currentItems[i];
            var existingLocalizedItem = FindMatchingItem(existingLocalizedItems, currentItem.ImageKey, i);

            result.Add(new ServiceListItem
            {
                ImageKey = currentItem.ImageKey,
                QrCodeKey = currentItem.QrCodeKey,
                Name = currentItem.Name,
                Description = currentItem.Description,
                Price = currentItem.Price
            });
        }

        return result;
    }

    private static bool EnsureItemKeys(List<ServiceListItem> items)
    {
        var changed = false;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ImageKey))
            {
                item.ImageKey = CreateImageKey();
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(item.QrCodeKey))
            {
                item.QrCodeKey = CreateQrCodeKey();
                changed = true;
            }
        }

        return changed;
    }

    private static string CreateQrCodeKey()
    {
        return $"Service_QR_{Guid.NewGuid():N}";
    }

    private static string CreateImageKey()
    {
        return $"Service_IMG_{Guid.NewGuid():N}";
    }
    #endregion
}
