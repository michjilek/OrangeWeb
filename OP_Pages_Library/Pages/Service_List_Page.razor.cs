using Microsoft.AspNetCore.Components;
using OP_Shared_Library.Services;

namespace OP_Pages_Library.Pages;

public partial class Service_List_Page : IDisposable
{
    #region Inejction
    [Inject]
    private TranslationsService Translations { get; set; }
    [Inject]
    private EditModeService EditModeService { get; set; }
    #endregion

    #region Private Properties
    private bool isLoaded = false;
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
    #endregion
}