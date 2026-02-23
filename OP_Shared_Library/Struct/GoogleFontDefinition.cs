namespace OP_Shared_Library.Struct;

// Class represents a single font option that can be selected from the Google Fonts catalog.
public class GoogleFontDefinition
{
    public string Family { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string CssImportUrl { get; init; } = string.Empty;

    public string FallbackFontFamily { get; init; } = "sans-serif";

    private const string SamplePreviewText = "Žluťoučký kůň.";

    public string DisplayLabel
    {
        get
        {
            // If Category is empty, get only Family
            var baseLabel = string.IsNullOrWhiteSpace(Category) ? Family : $"{Family} ({Category})";

            return $"{baseLabel} — {SamplePreviewText}";
        }
    }
}