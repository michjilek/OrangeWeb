using Microsoft.AspNetCore.Components;
using OP_Shared_Library.Struct;

namespace OP_Pages_Library.Pages;

public partial class Home_Page : IDisposable
{
    #region Private Properties
    [Inject]
    private TranslationsService Translations { get; set; }
    #endregion

    #region Private Properties
    private bool isLoaded = false;
    private bool isHeroImageLoaded = false;
    #endregion

    #region Protected Properties
    protected List<ChangeTextItem> ChangeTextItemList { get; private set; }
    #endregion

    #region Ctor
    protected override async Task OnInitializedAsync()
    {
        await Translations.LoadAsync();

        EditModeService.OnEditModeChanged += HandleEditModeChanged;

        isLoaded = true;
    }
    public void Dispose()
    {
        EditModeService.OnEditModeChanged -= HandleEditModeChanged;
    }
    #endregion

    #region Handle
    private void HandleEditModeChanged()
    {
        // Redraw the component when the edit mode changes
        InvokeAsync(StateHasChanged);
    }

    private void HandleHeroImageLoaded()
    {
        if (!isHeroImageLoaded)
        {
            isHeroImageLoaded = true;
        }
    }
    #endregion
}
