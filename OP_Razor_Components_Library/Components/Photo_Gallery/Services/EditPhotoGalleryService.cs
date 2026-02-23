using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;
using System.Text;
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
    private const string FileName_orig = "photoGallery.yaml";
    private const string FileName_en = "photoGallery_en.yaml";
    private string FileName;
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
        _filePath = $"/data/{FileName}";
        _url = $"{_navigation.BaseUri}{_filePath}";
    }
    // Normalize all references
    private void NormalizeImageReferences()
    {
        if (Items is null || !Items.Any()) return;

        foreach (var item in Items)
        {
            item.ImagePath = _minIoService.NormalizeImageReference(item.ImagePath);
        }
    }
    // Refresh all public urls
    private async Task RefreshSignedUrlsAsync()
    {
        foreach (var item in Items)
        {
            // Normalize base url
            item.ImagePath = _minIoService.NormalizeImageReference(item.ImagePath);

            // Get public url
            item.ImageSignedUrl = await ResolveImageUrlAsync(item.ImagePath);
        }
    }
    #endregion

    #region Public Methods
    // Read items from file to List
    public async Task LoadAsync(string language)
    {
        ChangeLangFile(language);

        try
        {
            // Send GET to url and take stream from it
            using var stream = await _httpClient.GetStreamAsync(_url);

            // Get GalleryImage Items from yaml by url and stream
            Items = _yamlService.LoadYamlList<GalleryImage>(_url, stream) ?? new();
        }
        catch (Exception ex)
        {
            _customLogger.MyLogger.Error($"EditPhotoGalleryService: LoadAsync: Unable to read '{_url}': {ex.Message}");
            Items = new();
        }

        // Normalize all references
        NormalizeImageReferences();

        // Refresh public url
        await RefreshSignedUrlsAsync();
    }
    // Save items to file
    public async Task SaveAsync()
    {
        await _yamlService.SaveToYamlAsync(Items, FileName);
    }
    // Add Item
    public void AddNewItem()
    {
        Items.Add(new GalleryImage
        {
            Id = Guid.NewGuid(),
            ImagePath = "",
            ImageTitle = ""
        });
    }
    // RemoveItem
    public void RemoveItem(GalleryImage item)
    {
        Items.Remove(item);
    }
    public void ChangeLangFile(string language)
    {
        FileName = language.ToLower() == "cs" ? FileName_orig : FileName_en;

        ReBuildPaths();
    }
    // Get public url from minIo
    public async Task<string> ResolveImageUrlAsync(string imageReference)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            return null;
        }

        var normalizedReference = _minIoService.NormalizeImageReference(imageReference);

        try
        {
            // Get pre signed from MinIo
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
    #endregion
}
