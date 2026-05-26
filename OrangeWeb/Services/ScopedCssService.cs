using OP_Shared_Library.Configurations;

namespace OrangeWeb.Services;

public class ScopedCssService
{
    #region Public static methods
    // Build Initial Scoped Link Method
    public List<string> BuildInitialScopedCssLinks(CompanyBrandingOptions branding, string path)
    {
        var links = new List<string>();

        // Layout and shell components are rendered around every route, so their isolated CSS is always needed.
        AddCommonScopedCss(links, branding);

        // Add only the isolated CSS used by the current route. This keeps first-load CSS much smaller.
        switch (path)
        {
            case "/":
                AddHomeScopedCss(links);
                break;
            case "/about_us":
                AddIfMissing(links, "_content/OP_Pages_Library/Pages/About_Us_Page.razor.rz.scp.css");
                break;
            case "/aktuality":
                AddIfMissing(links, "_content/OP_Pages_Library/Pages/Actualities_Page.razor.rz.scp.css");
                AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/Actualities/Actualities.razor.rz.scp.css");
                break;
            case "/contacts":
                AddIfMissing(links, "_content/OP_Pages_Library/Pages/Contacts_Page.razor.rz.scp.css");
                AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/QuestionForm/QuestionForm.razor.rz.scp.css");
                break;
            case "/photo_gallery":
                AddIfMissing(links, "_content/OP_Pages_Library/Pages/Photo_Gallery_Page.razor.rz.scp.css");
                AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/Photo_Gallery/Photo_Gallery.razor.rz.scp.css");
                break;
            case "/reference":
                AddIfMissing(links, "_content/OP_Pages_Library/Pages/References_Page.razor.rz.scp.css");
                AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/References/References.razor.rz.scp.css");
                break;
            case "/service_list":
                AddIfMissing(links, "_content/OP_Pages_Library/Pages/Service_List_Page.razor.rz.scp.css");
                AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/ServiceList/ServiceList.razor.rz.scp.css");
                break;
            case "/login":
                AddIfMissing(links, "Pages/LoginPage.razor.rz.scp.css");
                AddIfMissing(links, "Components/Login/Login.razor.rz.scp.css");
                break;
        }

        return links;
    }
    #endregion

    #region Private static methods
    // Add Commont Scoped  Css Method
    void AddCommonScopedCss(List<string> links, CompanyBrandingOptions branding)
    {
        // Common scoped CSS used by the shared layout and components visible on most pages.
        AddIfMissing(links, "_content/OP_Pages_Library/Shared/MainLayout.razor.rz.scp.css");
        AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/EditableImage/EditableImage.razor.rz.scp.css");
        AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/EditableText/EditableText.razor.rz.scp.css");
        AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/Footer/Footer.razor.rz.scp.css");
        AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/LanguageSwitcher/LanguageSwitcher.razor.rz.scp.css");
        AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/Navigation/Navigation.razor.rz.scp.css");

        // Social icon CSS is only needed when that icon is actually configured for this brand.
        if (!string.IsNullOrWhiteSpace(branding.SocialLinks.Google))
        {
            AddIfMissing(links, "_content/OP_Razor_Image_Library/Social_sites/Google.razor.rz.scp.css");
        }
        if (!string.IsNullOrWhiteSpace(branding.SocialLinks.Seznam))
        {
            AddIfMissing(links, "_content/OP_Razor_Image_Library/Social_sites/Seznam.razor.rz.scp.css");
        }
        if (!string.IsNullOrWhiteSpace(branding.SocialLinks.Facebook))
        {
            AddIfMissing(links, "_content/OP_Razor_Image_Library/Social_sites/Facebook.razor.rz.scp.css");
        }
        if (!string.IsNullOrWhiteSpace(branding.SocialLinks.Instagram))
        {
            AddIfMissing(links, "_content/OP_Razor_Image_Library/Social_sites/Instagram.razor.rz.scp.css");
        }
    }

    void AddHomeScopedCss(List<string> links)
    {
        // The home page is the Lighthouse-critical route, so keep this list explicit and small.
        AddIfMissing(links, "_content/OP_Pages_Library/Pages/Home_Page.razor.rz.scp.css");
        AddIfMissing(links, "_content/OP_Razor_Components_Library/Components/News/News.razor.rz.scp.css");
    }

    void AddIfMissing(List<string> links, string href)
    {
        // Some route-specific components can also be part of the common shell; avoid duplicate requests.
        if (links.IndexOf(href) < 0)
        {
            links.Add(href);
        }
    }
    #endregion
}
