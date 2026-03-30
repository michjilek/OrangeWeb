using Microsoft.AspNetCore.Components;

namespace OP_Razor_Components_Library.Components.References.Services;

public class EditReferencesService
{
    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;
    private readonly IYamlService _yamlService;
    private readonly IMinIoService _minIoService;
    private readonly ICustomLogger _customLogger;

    private const string FileNameCs = "references.yaml";
    private const string FileNameEn = "references_en.yaml";
    private string _fileName = FileNameCs;

    public const string MinIoFolder = "references";

    public List<ReferenceItem> Items { get; private set; } = new();

    public EditReferencesService(HttpClient http,
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
        ChangeLangFile(language);

        var yamlPath = _yamlService.EnsureYamlPath(_fileName);

        try
        {
            if (File.Exists(yamlPath))
            {
                await using var fileStream = File.OpenRead(yamlPath);
                Items = _yamlService.LoadYamlList<ReferenceItem>(yamlPath, fileStream) ?? new();
            }
            else
            {
                Items = new();
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
        await _yamlService.SaveToYamlAsync(Items, _fileName);
    }

    public void RemoveItem(ReferenceItem item)
    {
        Items.Remove(item);
    }

    public void ChangeLangFile(string language)
    {
        _fileName = language?.ToLowerInvariant() == "cs" ? FileNameCs : FileNameEn;
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
            _customLogger.MyLogger.Error($"EditReferencesService: ResolveImageUrlAsync: Unable to build public URL for '{imageReference}': {ex.Message}");
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
}
