using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using OP_Shared_Library.Configurations;
using Serilog;

namespace OP_Pages_Library.Shared;
public partial class MainLayout : IDisposable
{
    #region Parameters
    [Parameter] public string MainTitle { get; set; } = "OP Template";
    #endregion

    #region Dependency Injection
    [Inject] private IOptions<CompanyBrandingOptions> BrandingOptions { get; set; }
    [Inject] private TranslationsService Translations { get; set; }
    #endregion

    #region Private Properties
    private CompanyBrandingOptions Branding => BrandingOptions?.Value ?? new CompanyBrandingOptions();
    private string currentLang = "cs"; // nebo si to vezmi z TranslationsService
    #endregion

    #region Ctor
    protected override async Task OnInitializedAsync()
    {
        await Translations.LoadAsync();

        EditModeService.OnEditModeChanged += HandleEditModeChanged;
    }
    public void Dispose()
    {
        EditModeService.OnEditModeChanged -= HandleEditModeChanged;
    }
    #endregion

    #region Public
    public void OnlineStatusChanged(bool isOnline)
    {
        // Save state to Globals
        Globals.Instance.IsOnlineGlobal = isOnline;
    }
    public void OnLanguageChanged(string newLang)
    {
        currentLang = newLang;
        // zavolej svou metodu zde
        // napø. TranslationsService.SetLanguage(newLang);
        // NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true); // pøípadnì refresh
        Log.Information($"Language changed to {newLang}");
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
    #endregion
}