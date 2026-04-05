using Microsoft.AspNetCore.Components;
using OP_Shared_Library.Struct;
using System.Globalization;
using System.Text;

public class TranslationsService
{
    #region Dependency Injection
    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;
    private readonly IYamlService _yamlService;
    private readonly ICustomLogger _customLogger;
    #endregion

    #region Private Properties
    private string _language = "cs";
    private readonly SemaphoreSlim _lock = new(1, 1);
    #endregion

    #region Public Properties
    public List<ChangeTextItem> ChangeTextItemList { get; private set; } = new();
    #endregion

    #region Ctor
    public TranslationsService(HttpClient http,
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
    public Task LoadAsync()
    {
        return LoadCoreAsync(_language);
    }
    public async Task LoadCoreAsync(string language)
    {
        // We need to use Lock because of possible concurrent calls
        await _lock.WaitAsync();

        try
        {
            try
            {
                var file = $"{language}.yaml";
                var yamlPath = _yamlService.EnsureYamlPath(file);

                // Primary source: resolved YAML path from YamlService
                // (external Configs/<brand>/Data when configured, else wwwroot/data).
                if (File.Exists(yamlPath))
                {
                    await using var fileStream = File.OpenRead(yamlPath);
                    ChangeTextItemList = _yamlService.LoadYamlList<ChangeTextItem>(yamlPath, fileStream) ?? new List<ChangeTextItem>();
                }

                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                _customLogger.MyLogger.Error($"TranslationService: LoadCoreAsync: {ex}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    public string GetText(string name)
    {
        return ChangeTextItemList.FirstOrDefault(x => x.Id == name)?.Translation ?? $"[{name}]";
    }
    public async Task ChangeLanguageAsync(string language)
    {
        _language = language;

        SetCulture(language);

        await LoadCoreAsync(language);
    }
    public async Task UpdateTranslationAsync(string id, string newText, string webRootPath)
    {
        var item = ChangeTextItemList.FirstOrDefault(x => x.Id == id);

        if (item != null)
        {
            item.Translation = newText;
            var yaml = _yamlService.SerializeYaml(ChangeTextItemList);

            var path = _yamlService.EnsureYamlPath($"{_language}.yaml");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, yaml, Encoding.UTF8);

            await LoadCoreAsync(_language);
        }
    }
    public string GetLanguage()
    {
        return _language;
    }
    #endregion

    #region Private Methods
    private void SetCulture(string lang)
    {
        var cultureName = lang switch
        {
            "cs" => "cs-CZ",
            "en" => "en-US",
            _ => "cs-CZ",
        };

        var culture = CultureInfo.GetCultureInfo(cultureName);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
    #endregion

    #region Events
    // Alert other component, that this service has changed
    public event Action OnChange;

    private void NotifyStateChanged() => OnChange?.Invoke();
    #endregion
}
