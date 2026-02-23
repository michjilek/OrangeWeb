using Microsoft.AspNetCore.Components;
using System.Text;

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
    private const string FileName_orig = "news.yaml";
    private const string FileName_en = "news_en.yaml";
    private string FileName;
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
        // Change file by lang
        ChangeLangFile(language);

        // Get items from yaml
        var filePath = $"/data/{FileName}";
        var url = $"{_navigation.BaseUri}{filePath}";
        try
        {
            using var stream = await _http.GetStreamAsync(url);
            Items = _yamlService.LoadYamlList<NewsItem>(url, stream) ?? new();
        }
        catch
        {
            Items = new();
        }

        // MinIo
        await RefreshSignedUrlsAsync();
    }
    public async Task SaveAsync(string webRootPath)
    {
        // Yaml
        var yaml = _yamlService.SerializeYaml(Items);
        var path = Path.Combine(webRootPath, "wwwroot", "data", FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, yaml, Encoding.UTF8);
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
            ImageSignedUrl=""
        });
    }
    public void RemoveItem(NewsItem item)
    {
        Items.Remove(item);
    }
    public void ChangeLangFile(string language)
    {
        FileName = language.ToLower() == "cs" ? FileName_orig : FileName_en;
    }
    // Resolve Image URL
    public async Task<string> ResolveImageUrlAsync(string imageReference)
    {
        // Get normalized image reference
        var normalizedImageReference = _minIoService.NormalizeImageReference(imageReference);

        // If isnt filled yet, get from minIo
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
    // Refresh public urls in MinIo
    private async Task RefreshSignedUrlsAsync()
    {
        foreach (var item in Items)
        {
            item.ImageSignedUrl = await ResolveImageUrlAsync(item.ImageUrl);
        }
    }
    #endregion
}
