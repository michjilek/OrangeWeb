using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
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
    [Inject] private IConfiguration Configuration { get; set; }
    #endregion

    #region Private Properties
    private static readonly HashSet<string> ImageColumnKeys = new(StringComparer.OrdinalIgnoreCase) { "image", "img", "photo", "picture" };
    private static readonly HashSet<string> ServiceColumnKeys = new(StringComparer.OrdinalIgnoreCase) { "service", "name", "title" };
    private static readonly HashSet<string> DescriptionColumnKeys = new(StringComparer.OrdinalIgnoreCase) { "description", "desc", "text" };
    private static readonly HashSet<string> PriceColumnKeys = new(StringComparer.OrdinalIgnoreCase) { "price", "cost" };
    private static readonly HashSet<string> QrCodeColumnKeys = new(StringComparer.OrdinalIgnoreCase) { "qrcode", "qr", "qr-code", "qr_code" };

    private bool isSaving;
    private bool showToast;
    private string toastMessage = string.Empty;         // nap�. "Ulo�eno ?" / "Chyba p�i ukl�d�n�"
    private bool toastIsError;                          // true => error, false => success
    private string ToastBgClass => toastIsError ? "text-bg-danger" : "text-bg-success";
    private bool isQrModalOpen;
    private string selectedQrImageSrc;
    private string selectedQrImageAlt;
    private HashSet<string> hiddenColumns = new(StringComparer.OrdinalIgnoreCase);

    private bool ShowImageColumn => !IsColumnHidden(ImageColumnKeys);
    private bool ShowServiceColumn => !IsColumnHidden(ServiceColumnKeys);
    private bool ShowDescriptionColumn => !IsColumnHidden(DescriptionColumnKeys);
    private bool ShowPriceColumn => !IsColumnHidden(PriceColumnKeys);
    private bool ShowQrCodeColumn => !IsColumnHidden(QrCodeColumnKeys);

    #endregion

    #region Ctor
    protected override async Task OnInitializedAsync()
    {
        hiddenColumns = GetHiddenColumns("Services:HiddenColumns");

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
            toastMessage = TranslationsService.GetText("Saved");      // p�idej si kl�� do YAML (nap�. "Ulo�eno ?")
            toastIsError = false;
        }
        catch
        {
            toastMessage = TranslationsService.GetText("Error");  // p�idej kl�� (nap�. "Chyba p�i ukl�d�n�")
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

    private bool IsColumnHidden(HashSet<string> aliases)
    {
        return hiddenColumns.Overlaps(aliases);
    }

    private HashSet<string> GetHiddenColumns(string sectionPath)
    {
        var values = Configuration.GetSection(sectionPath).Get<string[]>() ?? Array.Empty<string>();

        return new HashSet<string>(
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    private void OpenQrModal(string imageSrc, string imageAlt)
    {
        if (string.IsNullOrWhiteSpace(imageSrc))
        {
            return;
        }

        selectedQrImageSrc = imageSrc;
        selectedQrImageAlt = imageAlt;
        isQrModalOpen = true;
    }

    private void CloseQrModal()
    {
        isQrModalOpen = false;
        selectedQrImageSrc = null;
        selectedQrImageAlt = null;
    }

    private static string GetQrImageAlt(ServiceListItem item)
    {
        return string.IsNullOrWhiteSpace(item?.Name) ? "QR code" : $"{item.Name} QR code";
    }

    private static string GetServiceImageAlt(ServiceListItem item)
    {
        return string.IsNullOrWhiteSpace(item?.Name) ? "Service image" : $"{item.Name} image";
    }

    private static string GetQrOpenLabel(ServiceListItem item)
    {
        return string.IsNullOrWhiteSpace(item?.Name) ? "Open QR code" : $"Open QR code for {item.Name}";
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
    public string ImageKey { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Price { get; set; }
    public string QrCodeKey { get; set; }
}
#endregion
