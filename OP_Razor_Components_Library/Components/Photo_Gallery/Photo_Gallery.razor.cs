using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using OP_Razor_Components_Library.Components.Photo_Gallery.Services;
using OP_Shared_Library.Services;
using OP_Shared_Library.Struct;
using System.Linq;

namespace OP_Razor_Components_Library.Components.Photo_Gallery;

public partial class Photo_Gallery : IDisposable
{
    #region Parameters
    [Parameter] public EventCallback<bool> ModalToggled { get; set; }
    #endregion

    #region Private Properties
    private IBrowserFile uploadedImage;
    private GalleryImage draft = new();
    private string draftPreview;
    // Busy state
    private bool isBusy;
    private int uploadProgress = 0;
    private bool isAwaitingFile;
    #endregion

    #region Constants
    private const int PreviewMaxWidth = 1600;
    #endregion

    #region Protected Properties
    protected bool IsModalOpen = false;
    protected bool IsNew = false;
    protected GalleryImage SelectedImage;
    protected double startX;
    #endregion

    #region Inject
    [Inject] private IJSRuntime JSRuntime { get; set; }
    [Inject] private TranslationsService TranslationService { get; set; }
    [Inject] private EditPhotoGalleryService EditPhotoGalleryService { get; set; }
    [Inject] private IMinIoService MinIoService { get; set; }
    [Inject] private ICustomLogger CustomLogger { get; set; }
    #endregion

    #region Lifecycle
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Focus modal for keyboard navigation
        if (IsModalOpen && !EditModeService.IsEditing)
        {
            await JSRuntime.InvokeVoidAsync("eval", "document.querySelector('.modal-content').focus();");
        }
    }
    protected override async Task OnInitializedAsync()
    {
        await TranslationService.LoadAsync();
        await EditPhotoGalleryService.LoadAsync(TranslationService.GetLanguage());

        EditModeService.OnEditModeChanged += HandleEditModeChanged;
        TranslationService.OnChange += TranslationChanged;
    }
    public void Dispose()
    {
        EditModeService.OnEditModeChanged -= HandleEditModeChanged;
        TranslationService.OnChange -= TranslationChanged;
    }
    #endregion

    #region Private Methods
    private string GetCommentFromMetadata(Stream stream)
    {
        var directories = ImageMetadataReader.ReadMetadata(stream);
        var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

        // 0x9C9C = Windows XP Comment
        var comment = exifIfd0Directory?.GetDescription(0x9C9C);
        return string.IsNullOrEmpty(comment) ? string.Empty : comment;
    }
    // Upload image to MinIo and return object key and exif comment
    private async Task<(string objectKey, string exifOComment)> UploadImageToMinIoAsync(IBrowserFile file)
    {
        // Check if we have MinIo Service
        if (MinIoService is null)
        {
            CustomLogger.MyLogger.Error($"Photo_Gallery.razor.cs: UploadImageToMinIoAsync: MinIo service is not configured.");
        }

        // Ensure Bukcet in MinIo
        await MinIoService.EnsureBucketAsync();

        // Get Extension
        var extension = Path.GetExtension(file.Name);

        // Check taken extension
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        // Read file to memory stream
        var objectName = ResponsiveImageHelper.BuildResponsiveObjectKey(
            EditPhotoGalleryService.MinIoFolder,
            ResponsiveImageHelper.GetResponsiveUploadExtension());

        string exifComment = null;

        try
        {
            await using var metadataReadStream = file.OpenReadStream(10_000_000); // 10 MB limit
            using var metadataMs = new MemoryStream(); // Temporary stream for metadata extraction
            await metadataReadStream.CopyToAsync(metadataMs); // Copy to memory stream
            using var metadataStream = new MemoryStream(metadataMs.ToArray()); // Create a new stream for metadata extraction

            // Get comment from stream
            exifComment = GetCommentFromMetadata(metadataStream);
        }
        catch
        {
            CustomLogger.MyLogger.Warning($"Photo_Gallery.razor.cs: UploadImageToMinIoAsync: read comment issue. ");
        }

        // Upload to MinIo
        await UploadResponsiveVariantAsync(file, objectName);

        // return value tuple
        return (objectName, exifComment);
    }
    private async Task SaveEdit()
    {
        if (IsNew)
        {
            await SaveNewItem();
            return;
        }

        // Check null of selected image
        if (SelectedImage is null) return;

        // Set comment
        string exifComment = null;

        // Get object key from Image path
        string objectKey = SelectedImage.ImagePath;

        // If it is not null
        if (uploadedImage != null)
        {
            // Save to MinIo
            var uploadResult = await UploadImageToMinIoAsync(uploadedImage);

            // Get ObjectKey
            objectKey = uploadResult.objectKey;

            // Get comment
            exifComment = uploadResult.exifOComment;
        } // If draft is not null, get draft
        else if (!string.IsNullOrWhiteSpace(draft.ImagePath) &&
                 !draft.ImagePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            objectKey = draft.ImagePath;
        }

        // Set comment to draft title
        if (!string.IsNullOrWhiteSpace(exifComment) && string.IsNullOrWhiteSpace(draft.ImageTitle))
        {
            draft.ImageTitle = exifComment;
        }

        // Normalize object key
        objectKey = EditPhotoGalleryService.NormalizeImageReference(objectKey);

        // Set Selected image and draft paths
        SelectedImage.ImagePath = objectKey ?? string.Empty;
        SelectedImage.ImageSignedUrl = await EditPhotoGalleryService.ResolveImageUrlAsync(SelectedImage.ImagePath);

        // Set title
        SelectedImage.ImageTitle = draft.ImageTitle ?? string.Empty;

        draft.ImagePath = SelectedImage.ImagePath;

        // Refresh Preview
        draftPreview = SelectedImage.ImageSignedUrl ?? SelectedImage.ImagePath;

        // Save Async
        await EditPhotoGalleryService.SaveAsync();

        // Close modal
        IsModalOpen = false;
        IsNew = false;

        // Clear uploaded image
        uploadedImage = null;
    }
    private async Task SaveNewItem()
    {
        // Create default new Gallery Image
        var newItem = new GalleryImage
        {
            Id = Guid.NewGuid(),
            ImageTitle = draft.ImageTitle ?? string.Empty,
            ImagePath = string.Empty
        };
        
        // If uploaded image is any
        if (uploadedImage != null)
        {
            // Upload to MinIo
            var uploadResult = await UploadImageToMinIoAsync(uploadedImage);

            // Get object kex
            newItem.ImagePath = uploadResult.objectKey;

            // If get comment, save it to image title
            if (string.IsNullOrWhiteSpace(newItem.ImageTitle) && !string.IsNullOrWhiteSpace(uploadResult.exifOComment))
            {
                newItem.ImageTitle = uploadResult.exifOComment;
            }
        }
        // If image is null and exists some draft image path, set it to image path
        else if (!string.IsNullOrWhiteSpace(draft.ImagePath) &&
                 !draft.ImagePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            newItem.ImagePath = draft.ImagePath;
        }

        // Normalize Image Path
        newItem.ImagePath = EditPhotoGalleryService.NormalizeImageReference(newItem.ImagePath);

        // Get signed url
        newItem.ImageSignedUrl = await EditPhotoGalleryService.ResolveImageUrlAsync(newItem.ImagePath);

        // Set path to draft
        draft.ImagePath = newItem.ImagePath;

        draft.ImageTitle = newItem.ImageTitle;

        // Set draft preview
        draftPreview = newItem.ImageSignedUrl ?? newItem.ImagePath;

        // Add to gallery items
        EditPhotoGalleryService.Items.Add(newItem);

        // Set selected image
        SelectedImage = newItem;

        // Save async
        await EditPhotoGalleryService.SaveAsync();

        // Close modal
        IsModalOpen = false;
        IsNew = false;

        // Clear uploaded image
        uploadedImage = null;
    }
    private async Task Delete()
    {
        if (SelectedImage is null || EditPhotoGalleryService.Items.Count == 0) return;

        EditPhotoGalleryService.RemoveItem(SelectedImage);
        await EditPhotoGalleryService.SaveAsync();

        IsModalOpen = false;
        uploadedImage = null;
        draftPreview = null;
    }
    private void OpenNew()
    {
        SelectedImage = null;
        draft = new GalleryImage
        {
            Id = Guid.NewGuid(),
            ImageTitle = "",
            ImagePath = ""
        };

        uploadedImage = null;
        draftPreview = string.Empty;
        IsNew = true;
        IsModalOpen = true;

        _ = ModalToggled.InvokeAsync(true);
    }
    private void BeginUpload()
    {
        // Show loader before dialog is open
        isBusy = true;
        isAwaitingFile = true;
        uploadProgress = 0;
        StateHasChanged();
    }
    private void CancelBusy()
    {
        isBusy = false;
        isAwaitingFile = false;
        uploadProgress = 0;
    }
    #endregion

    #region Protected Methods
    protected string GetImageSource(GalleryImage image)
    {
        return image is null
               ? string.Empty
               : !string.IsNullOrWhiteSpace(image.ImageSignedUrl)
                   ? image.ImageSignedUrl
                   : image.ImagePath;
    }
    protected async Task OpenImage(GalleryImage galleryImage)
    {
        SelectedImage = galleryImage;
        draft = new GalleryImage
        {
            Id = galleryImage.Id,
            ImageTitle = galleryImage.ImageTitle,
            ImagePath = galleryImage.ImagePath
        };

        draftPreview = GetImageSource(galleryImage);

        IsNew = false;
        IsModalOpen = true;
        await JSRuntime.InvokeVoidAsync("document.body.classList.add", "modal-open");
        await ModalToggled.InvokeAsync(true);

    }
    protected async Task CloseModal()
    {
        IsModalOpen = false;
        IsNew = false;
        await JSRuntime.InvokeVoidAsync("document.body.classList.remove", "modal-open");
        await ModalToggled.InvokeAsync(false);
    }
    protected void Move(KeyboardEventArgs e)
    {
        if (EditPhotoGalleryService.Items.Count == 0) return;

        if (e.Key == "ArrowRight") MoveNext();
        else if (e.Key == "ArrowLeft") MovePrevious();
        else if (e.Key == "Escape" || e.Key == "Esc")
            _ = InvokeAsync(CloseModal);
    }
    private void MoveNext()
    {
        var currentIndex = EditPhotoGalleryService.Items.IndexOf(SelectedImage);
        var nextIndex = (currentIndex + 1) % EditPhotoGalleryService.Items.Count;
        SelectedImage = EditPhotoGalleryService.Items[nextIndex];
    }
    private void MovePrevious()
    {
        var currentIndex = EditPhotoGalleryService.Items.IndexOf(SelectedImage);
        var prevIndex = (currentIndex - 1 + EditPhotoGalleryService.Items.Count) % EditPhotoGalleryService.Items.Count;
        SelectedImage = EditPhotoGalleryService.Items[prevIndex];
    }
    protected void HandleTouchStart(TouchEventArgs e) => startX = e.Touches[0].ClientX;
    protected void HandleTouchEnd(TouchEventArgs e)
    {
        double endX = e.ChangedTouches[0].ClientX;

        if (startX - endX > 50) MoveNext();
        else if (endX - startX > 50) MovePrevious();
    }
    // Build SrcSet for responsive images
    protected string GetImageSrcSet(GalleryImage image)
    {
        return ResponsiveImageHelper.BuildSrcSet(MinIoService, image.ImagePath);
    }
    // Get sizes attribute for responsive images
    protected string GetResponsiveSizes(string srcSet)
    {
        return string.IsNullOrWhiteSpace(srcSet) ? null : "100vw";
    }
    #endregion

    #region Handlers
    private async Task HandleImageChange(InputFileChangeEventArgs e)
    {
        uploadedImage = e.File;

        isAwaitingFile = false;
        isBusy = true;
        uploadProgress = 0;
        StateHasChanged();
        // To draw overlay
        await Task.Delay(1);

        try
        {
            var preview = await uploadedImage.RequestImageFileAsync(uploadedImage.ContentType, PreviewMaxWidth, ResponsiveImageHelper.MaxUploadedHeight);
            await using var stream = preview.OpenReadStream(maxAllowedSize: 10_000_000);
            using var ms = new MemoryStream();

            var buffer = new byte[80 * 1024];
            long readTotal = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await ms.WriteAsync(buffer, 0, read);
                readTotal += read;
                uploadProgress = (int)(readTotal * 100 / preview.Size);

                if (readTotal % (512 * 1024) == 0)
                    StateHasChanged();
            }

            draftPreview = $"data:{preview.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
            uploadProgress = 100;
        }
        finally
        {
            isBusy = false;
            isAwaitingFile = false;
            StateHasChanged();
        }
    }
    private void HandleEditModeChanged() => InvokeAsync(StateHasChanged);
    private async void TranslationChanged()
    {
        await EditPhotoGalleryService.LoadAsync(TranslationService.GetLanguage());
        StateHasChanged();
    }
    private async Task UploadResponsiveVariantAsync(IBrowserFile file, string objectKey)
    {
        var maxWidth = ResponsiveImageHelper.VariantWidths.Max();

        foreach (var width in ResponsiveImageHelper.VariantWidths)
        {
            var resized = await ResponsiveImageHelper.RequestResponsiveVariantAsync(file, width);
            await using var readStream = resized.OpenReadStream(10_000_000);
            using var ms = new MemoryStream();
            await readStream.CopyToAsync(ms);
            ms.Position = 0;

            var key = width == maxWidth ? objectKey : ResponsiveImageHelper.BuildVariantKey(objectKey, width);
            await MinIoService.PutObjectAsync(key, ms, resized);
        }
    }
    #endregion
}
