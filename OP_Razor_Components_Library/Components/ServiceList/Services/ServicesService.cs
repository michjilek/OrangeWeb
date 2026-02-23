using Microsoft.AspNetCore.Components;
using OP_Razor_Components_Library.Components.ServiceList;
using OP_Shared_Library;
using OP_Shared_Library.Struct;
using System.Text;

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
                var filePath = $"/data/{FileName}";
                var url = $"{_navigation.BaseUri}{filePath}";
                using var stream = await _http.GetStreamAsync(url);

                Items = _yamlService.LoadYamlList<ServiceListItem>(url, stream);
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
        var yaml = _yamlService.SerializeYaml(Items);
        var path = Path.Combine(webRootPath, "wwwroot", "data", FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, yaml, Encoding.UTF8);
    }
    public void AddNewItem()
    {
        Items.Add(new ServiceListItem { Name = "", Description = "", Price = "" });
    }
    public void RemoveItem(ServiceListItem item)
    {
        Items.Remove(item);
    }
    public void ChangeLangFile(string language)
    {
        FileName = language.ToLower() == "cs" ? FileName_orig : FileName_en;
    }
    #endregion
}
