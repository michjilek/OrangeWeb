using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using OP_Shared_Library.Configurations;
using Serilog;

namespace OP_Pages_Library.Shared;
public partial class MainLayout : IDisposable, IAsyncDisposable
{
    #region Parameters
    [Parameter] public string MainTitle { get; set; } = "OP Template";
    #endregion

    #region Dependency Injection
    [Inject] private IOptions<CompanyBrandingOptions> BrandingOptions { get; set; }
    [Inject] private TranslationsService Translations { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; }
    #endregion

    #region Private Properties
    private CompanyBrandingOptions Branding => BrandingOptions?.Value ?? new CompanyBrandingOptions();
    private string currentLang = "cs";
    private IJSObjectReference? _materialSymbolsModule;
    #endregion

    #region Ctor
    protected override async Task OnInitializedAsync()
    {
        await Translations.LoadAsync();
        currentLang = Translations.GetLanguage();

        EditModeService.OnEditModeChanged += HandleEditModeChanged;
        Translations.OnChange += HandleTranslationsChanged;
    }
    public void Dispose()
    {
        EditModeService.OnEditModeChanged -= HandleEditModeChanged;
        Translations.OnChange -= HandleTranslationsChanged;
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();

        if (_materialSymbolsModule is not null)
        {
            try
            {
                await _materialSymbolsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
    #endregion

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _materialSymbolsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/OP_Pages_Library/js/materialSymbolsLoader.js");

        await _materialSymbolsModule.InvokeVoidAsync("ensureMaterialSymbolsReady");
    }

    #region Public
    public void OnlineStatusChanged(bool isOnline)
    {
        // Save state to Globals
        Globals.Instance.IsOnlineGlobal = isOnline;
    }
    public async Task OnLanguageChanged(string newLang)
    {
        currentLang = newLang;
        await Translations.ChangeLanguageAsync(newLang);
        Log.Information($"Language changed to {newLang}");
        await InvokeAsync(StateHasChanged);
    }
    public void ToggleEditMode()
    {
        EditModeService.ToggleEditMode();
    }
    #endregion

    #region Handle
    private void HandleEditModeChanged()
    {
        // Redraw the component when the edit mode changes
        InvokeAsync(StateHasChanged);
    }
    private void HandleTranslationsChanged()
    {
        currentLang = Translations.GetLanguage();
        InvokeAsync(StateHasChanged);
    }
    #endregion
}
