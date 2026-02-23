namespace OP_Shared_Library.Configurations;

public sealed class CompanyBrandingOptions
{
    public string CompanyName { get; set; } = string.Empty;
    public string WebSiteTitle { get; set; } = string.Empty;
    public string AppleMobileWebAppTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string ContactFormSource { get; set; } = string.Empty;
    public CompanySocialLinks SocialLinks { get; set; } = new();
}

public sealed class  CompanySocialLinks
{
    public string Google { get; set; } = string.Empty;
    public string Seznam { get; set; } = string.Empty;
    public string Facebook { get; set; } = string.Empty;
    public string Instagram { get; set; } = string.Empty;
}
