using OP_Shared_Library.Struct;

namespace OP_Razor_Components_Library.Components.Navigation.Services;

public sealed class NavigationService
{
    // Holds the current navigation items as an array.
    // We keep it as an array (not List<T>) so the UI can safely enumerate it while rendering.
    // When items change, we replace the whole array (copy-on-write) instead of modifying it in-place.
    private NavigationItem[] _navigationItems = Array.Empty<NavigationItem>();

    public IReadOnlyList<NavigationItem> NavigationItems => _navigationItems;

    public event Action? OnChanged;

    public Task LoadAsync()
    {
        bool initialized = false;

        // Initialize only once.
        if (_navigationItems.Length == 0)
        {
            // Replace whole array (no in-place changes).
            _navigationItems = new[]
            {
                new NavigationItem { Id = Guid.NewGuid(), Href="/photo_gallery", TextId="Photogallery_menu_item", Order=10 },
                new NavigationItem { Id = Guid.NewGuid(), Href="/service_list",  TextId="Services_menu_item",     Order=20 },
                new NavigationItem { Id = Guid.NewGuid(), Href="/about_us",      TextId="About_us_menu_item",    Order=30 },
                new NavigationItem { Id = Guid.NewGuid(), Href="/contacts",      TextId="Contacts_menu_item",    Order=30 }
            };

            initialized = true;
        }

        // Notify only if something changed.
        if (initialized) OnChanged?.Invoke();

        return Task.CompletedTask;
    }
}
