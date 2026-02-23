using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;
using OP_Shared_Library.Services;
using System.Threading.Tasks;

namespace OP_Razor_Components_Library.Components.ServiceList;

public partial class ServiceList : IDisposable
{
    #region Inject
    [Inject] private ServicesService ServicesService { get; set; }
    [Inject] private TranslationsService TranslationsService { get; set; }
    [Inject] private EditModeService EditModeService { get; set; }
    [Inject] private IHostEnvironment Env { get; set; }
    #endregion

    #region Private Properties
    private bool isSaving;
    private bool showToast;
    private string toastMessage = string.Empty;         // napø. "Uloženo ?" / "Chyba pøi ukládání"
    private bool toastIsError;                          // true => error, false => success
    private string ToastBgClass => toastIsError ? "text-bg-danger" : "text-bg-success";

    #endregion

    #region Ctor
    protected override async Task OnInitializedAsync()
    {
        await TranslationsService.LoadAsync();
        await ServicesService.LoadAsync(TranslationsService.GetLanguage());

        TranslationsService.OnChange += TranslationChanged;
        EditModeService.OnEditModeChanged += HandleEditModeChanged;
    }
    #endregion

    #region Private Methods
    private void AddService()
    {
        ServicesService.AddNewItem();
    }
    private void RemoveService(ServiceListItem item)
    {
        ServicesService.RemoveItem(item);
    }
    private async Task Save()
    {
        if (isSaving) return;

        isSaving = true;
        showToast = false;
        StateHasChanged();

        try
        {
            await ServicesService.SaveAsync(Env.ContentRootPath);
            toastMessage = TranslationsService.GetText("Saved");      // pøidej si klíè do YAML (napø. "Uloženo ?")
            toastIsError = false;
        }
        catch
        {
            toastMessage = TranslationsService.GetText("Error");  // pøidej klíè (napø. "Chyba pøi ukládání")
            toastIsError = true;
        }
        finally
        {
            isSaving = false;
            showToast = true;
            StateHasChanged();
            _ = AutoHideToast();
        }
    }

    private async Task AutoHideToast()
    {
        await Task.Delay(2500);
        showToast = false;
        await InvokeAsync(StateHasChanged);
    }
    private void HandleEditModeChanged()
    {
        InvokeAsync(StateHasChanged);
    }
    #endregion

    #region Public Methods
    public void Dispose()
    {
        EditModeService.OnEditModeChanged -= HandleEditModeChanged;
        TranslationsService.OnChange -= TranslationChanged;
    }
    #endregion

    #region Handlers
    private async void TranslationChanged()
    {
        await ServicesService.LoadAsync(TranslationsService.GetLanguage());
        StateHasChanged();
    }
    #endregion
}

#region Other Classes
public class ServiceListItem
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Price { get; set; }
}
#endregion