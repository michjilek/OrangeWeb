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
    private const string FileName_orig = "servicelist.yaml";
    private const string FileName_en = "servicelist_en.yaml";
    private string FileName;
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
        ChangeLangFile(language);

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
                var yamlPath = _yamlService.EnsureYamlPath(FileName);

                if (File.Exists(yamlPath))
                {
                    await using var fileStream = File.OpenRead(yamlPath);
                    Items = _yamlService.LoadYamlList<ServiceListItem>(yamlPath, fileStream) ?? new();
                    if (EnsureQrCodeKeys())
                    {
                        await _yamlService.SaveToYamlAsync(Items, FileName);
                    }
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
        EnsureQrCodeKeys();
        await _yamlService.SaveToYamlAsync(Items, FileName);
    }
    public void AddNewItem()
    {
        Items.Add(new ServiceListItem
        {
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
    public void ChangeLangFile(string language)
    {
        FileName = language.ToLower() == "cs" ? FileName_orig : FileName_en;
    }

    private bool EnsureQrCodeKeys()
    {
        var changed = false;

        foreach (var item in Items)
        {
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
    #endregion
}
