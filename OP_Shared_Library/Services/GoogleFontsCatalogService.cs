using System.Globalization;
using System.Text;
using System.Text.Json;
using OP_Shared_Library.Struct;

namespace OP_Shared_Library.Services;

// Loads the catalog of available Google Fonts so administrators can choose a font instead of
// copying the embed information manually.
public class GoogleFontsCatalogService
{
    #region Constants
    // Google Fonts public metadata endpoint
    private const string CatalogUrl = "https://fonts.google.com/metadata/fonts";
    #endregion

    #region Private Static Properties
    // Web: Get Web preset: camelCase serialization, web, quoted numbers etc.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        // Be flexible with incoming JSON property casing
        PropertyNameCaseInsensitive = true
    };
    #endregion

    #region Private Properties
    private readonly IHttpClientFactory _httpClientFactory; // factory for creating typed HttpClient
    private readonly ICustomLogger _customLogger;
    private readonly SemaphoreSlim _lock = new(1, 1); // simple async lock to avoid concurrent loads
    private IReadOnlyList<GoogleFontDefinition> _cache = Array.Empty<GoogleFontDefinition>(); // cached catalog
    private bool _isLoaded; // indicates whether the cache has been initialized
    #endregion

    #region Ctor
    public GoogleFontsCatalogService(IHttpClientFactory httpClientFactory,
                                     ICustomLogger customLogger)
    {
        _httpClientFactory = httpClientFactory;
        _customLogger = customLogger;
    }
    #endregion

    #region Public Methods
    // Populate cache (thread safe)
    public async Task<IReadOnlyList<GoogleFontDefinition>> GetFontsAsync()
    {
        // Fast path: return cache if already loaded
        if (_isLoaded)
        {
            return _cache;
        }

        // Ensure only one load happens across concurrent callers
        await _lock.WaitAsync();
        try
        {
            if (!_isLoaded)
            {
                _cache = await LoadCatalogAsync(); // populate cache
                _isLoaded = true; // set the flag
            }
        }
        finally
        {
            _lock.Release();
        }

        return _cache;
    }
    private async Task<IReadOnlyList<GoogleFontDefinition>> LoadCatalogAsync()
    {
        try
        {
            // Create a named HTTP client
            var client = _httpClientFactory.CreateClient(nameof(GoogleFontsCatalogService));

            // Set HTTP HEADING User-Agent for all requests from created client
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LP-Solution Font Catalog Service");

            // Use headers-only buffering to reduce memory footprint on large payloads
            // Send HTTP GET asynchronously. Getting result after reading heading immidiately (ResponseHeadersRead).
            // -> it is reducing large payloads
            using var response = await client.GetAsync(CatalogUrl, HttpCompletionOption.ResponseHeadersRead);
            // Want onyl success content (200-299 codes), when 4xx and 5xx -> throw exception
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(); // read full body as string

            // Remove protective noise from json and serialize json to GoogleFontMetadata (list of GoogleFontMetadataItem)
            var metadata = JsonSerializer.Deserialize<GoogleFontsMetadata>(RemoveProctectiveNoiseFromPayload(payload), SerializerOptions);

            // If metadata is not null, but list is empty
            if (metadata?.FamilyMetadataList is not { Count: > 0 })
            {
                _customLogger.MyLogger.Warning("GoogleFontCatalogService: LoadCatalogAsync: Google Fonts metadata request succeeded but no fonts were returned.");
                return GetFallbackFonts(); // Get predefined items (only some of fonts, but at least we will send something)
            }

            // Map raw items -> domain definitions (unique + sorted)
            var fonts = metadata.FamilyMetadataList
                .Select(CreateDefinition) // convert to our definition
                .Where(static font => font is not null)
                .Select(static font => font!) // tell compilator, that font is not null, because of WHERE before
                .Distinct(new GoogleFontDefinitionComparer()) // distinct by family name (case-insensitive)
                .OrderBy(static font => font.Family, StringComparer.OrdinalIgnoreCase) // sort for stable UX
                .ToList();

            if (fonts.Count == 0)
            {
                _customLogger.MyLogger.Warning("Google Fonts metadata did not contain any usable font definitions.");
                return GetFallbackFonts();
            }

            return fonts; // happy path
        }
        catch (Exception ex)
        {
            // Any network/parse error -> fallback list to keep UI functional
            _customLogger.MyLogger.Warning(ex, "Failed to download Google Fonts catalog. Falling back to the baked-in list.");
            return GetFallbackFonts();
        }
    }
    #endregion

    #region Private Methods
    private static string RemoveProctectiveNoiseFromPayload(string payload)
    {
        // Google Fonts metadata is prefixed with XSSI protection: )]}\'\n
        if (string.IsNullOrWhiteSpace(payload))
        {
            return "{}"; // return empty JSON to avoid null
        }

        // Create new read only span
        var span = payload.AsSpan();

        // Remove the 5-character XSSI prefix
        // Some API sends protective noise to prevent hacking json - )]}'\n
        // Ordinal - without localization
        if (span.StartsWith(")]}\'".AsSpan(), StringComparison.Ordinal))
        {
            // Get string from fifth index more
            return span[5..].ToString();
        }

        return payload; // unchanged
    }
    private static GoogleFontDefinition CreateDefinition(GoogleFontsMetadataItem item)
    {
        // If family is not null, trim
        var family = item.Family?.Trim();

        // We dont want invalid family
        if (string.IsNullOrWhiteSpace(family))
        {
            return null;
        }

        // If variants are missing, default to regular (400)
        var variants = item.Variants ?? new List<string> { "regular" };
        var combos = ParseVariants(variants); // normalize into (italic,weight) pairs
        var cssUrl = BuildCssUrl(family, combos); // construct css2 import URL

        if (string.IsNullOrWhiteSpace(cssUrl))
        {
            return null; // if URL cannot be built, drop this item
        }

        var category = item.Category?.Trim() ?? string.Empty; // e.g., serif, sans-serif, display

        return new GoogleFontDefinition
        {
            Family = family,
            Category = category,
            CssImportUrl = cssUrl,
            FallbackFontFamily = DetermineFallback(category) // sensible generic fallback for preview
        };
    }
    private static IReadOnlyList<FontVariantCombo> ParseVariants(IEnumerable<string> variants)
    {
        var results = new HashSet<FontVariantCombo>(); // use set to avoid duplicates

        foreach (var variant in variants)
        {
            if (string.IsNullOrWhiteSpace(variant))
            {
                continue; // skip empties
            }

            var trimmed = variant.Trim().ToLowerInvariant();

            // Common shorthands in the metadata
            if (trimmed is "regular" or "400")
            {
                // (Italic, weight)
                results.Add(new FontVariantCombo(0, 400)); // normal, wght 400
                continue;
            }

            if (trimmed is "italic")
            {
                results.Add(new FontVariantCombo(1, 400)); // italic, wght 400
                continue;
            }

            // Detect NUMERIC ITALIC suffix like "700italic"
            var italic = trimmed.EndsWith("italic", StringComparison.Ordinal);
            var numericPart = italic ? trimmed[..^6] : trimmed; // remove "italic" suffix if present

            if (!int.TryParse(numericPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight))
            {
                continue; // ignore unexpected tokens
            }

            weight = Math.Clamp(weight, 100, 1000); //Cut weight between 100 and 1000 (it is some validation)
            results.Add(new FontVariantCombo(italic ? 1 : 0, weight));
        }

        // Ensure we always return at least one sensible variant
        if (results.Count == 0)
        {
            results.Add(new FontVariantCombo(0, 400));
        }

        // Sort by italic property first, then by weight
        return results.OrderBy(static combo => combo.Italic).ThenBy(static combo => combo.Weight).ToArray();
    }
    private static string BuildCssUrl(string family, IReadOnlyList<FontVariantCombo> combos)
    {
        // Build query
        var familyQuery = BuildFamilyQuery(family); // e.g., "Open+Sans"

        if (combos.Count == 0)
        {
            return string.Empty; // should not happen because we enforce default above
        }

        var hasItalic = combos.Any(static combo => combo.Italic == 1);
        var hasMultipleWeights = combos.Select(static combo => combo.Weight).Distinct().Skip(1).Any(); // at least 2 distinct weights

        var builder = new StringBuilder("https://fonts.googleapis.com/css2?");
        builder.Append("family=");
        builder.Append(familyQuery);

        if (hasItalic)
        {
            // CSS2 scheme for ital,wght with multiple pairs (e.g., 0,400;1,400;1,700)
            builder.Append(":ital,wght@");
            builder.Append(string.Join(";", combos.Select(static combo => $"{combo.Italic},{combo.Weight}")));
        }
        else if (hasMultipleWeights)
        {
            // CSS2 scheme for multiple weights (e.g., :wght@300;400;700)
            builder.Append(":wght@");
            builder.Append(string.Join(";", combos.Select(static combo => combo.Weight.ToString(CultureInfo.InvariantCulture))));
        }

        // Swap means that it is not waiting to font download, but use fallbackFont first, than, after load, use right font
        builder.Append("&display=swap"); // good rendering behavior hint
        return builder.ToString();
    }
    private static string BuildFamilyQuery(string family)
    {
        // Convert spaces to '+' and URL-encode each token
        var parts = family.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Every string is URL safe (string -> %20, & → %26, + → %2B etc.) and splitted by +
        return string.Join('+', parts.Select(Uri.EscapeDataString));
    }
    private static string DetermineFallback(string category)
    {
        // Map Google category to a reasonable CSS generic family
        return category?.ToLowerInvariant() switch
        {
            "serif" => "serif",
            "display" => "cursive",
            "handwriting" => "cursive",
            "monospace" => "monospace",
            _ => "sans-serif"
        };
    }
    private static IReadOnlyList<GoogleFontDefinition> GetFallbackFonts()
    {
        // Built-in minimal, safe set to keep the UI functional without network
        return new List<GoogleFontDefinition>
        {
            new()
            {
                Family = "Arimo",
                Category = "sans-serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Arimo&display=swap",
                FallbackFontFamily = "sans-serif"
            },
            new()
            {
                Family = "Roboto",
                Category = "sans-serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500;700&display=swap",
                FallbackFontFamily = "sans-serif"
            },
            new()
            {
                Family = "Open Sans",
                Category = "sans-serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Open+Sans:wght@300;400;600;700&display=swap",
                FallbackFontFamily = "sans-serif"
            },
            new()
            {
                Family = "Montserrat",
                Category = "sans-serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Montserrat:wght@300;400;600;700&display=swap",
                FallbackFontFamily = "sans-serif"
            },
            new()
            {
                Family = "Lato",
                Category = "sans-serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Lato:wght@300;400;700&display=swap",
                FallbackFontFamily = "sans-serif"
            },
            new()
            {
                Family = "Merriweather",
                Category = "serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Merriweather:wght@300;400;700&display=swap",
                FallbackFontFamily = "serif"
            },
            new()
            {
                Family = "Playfair Display",
                Category = "serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Playfair+Display:wght@400;600;700&display=swap",
                FallbackFontFamily = "serif"
            },
            new()
            {
                Family = "Raleway",
                Category = "sans-serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Raleway:wght@300;400;600;700&display=swap",
                FallbackFontFamily = "sans-serif"
            },
            new()
            {
                Family = "Poppins",
                Category = "sans-serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Poppins:wght@300;400;600;700&display=swap",
                FallbackFontFamily = "sans-serif"
            },
            new()
            {
                Family = "Source Sans Pro",
                Category = "sans-serif",
                CssImportUrl = "https://fonts.googleapis.com/css2?family=Source+Sans+Pro:wght@300;400;600;700&display=swap",
                FallbackFontFamily = "sans-serif"
            }
        };
    }
    #endregion

    #region Records
    // Minimal immutable record to represent (italic, weight) combo for css2 query
    private sealed record FontVariantCombo(int Italic, int Weight);
    #endregion

    #region Class
    // Equality comparer to distinct fonts by Family (case-insensitive)
    private sealed class GoogleFontDefinitionComparer : IEqualityComparer<GoogleFontDefinition>
    {
        public bool Equals(GoogleFontDefinition x, GoogleFontDefinition y)
        {
            // Are x and y object same?
            if (ReferenceEquals(x, y))
            {
                return true; // same reference
            }

            // Is one of theme null?
            if (x is null || y is null)
            {
                return false; // null mismatch
            }

            // If x and y are not same objects, we compare family Names with case insensitive
            return string.Equals(x.Family, y.Family, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(GoogleFontDefinition obj)
        {
            // If equals x and y is true, they must have same hash
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Family);
        }
    }
    // DTOs representing the subset of Google Fonts metadata we care about
    private sealed class GoogleFontsMetadata
    {
        public List<GoogleFontsMetadataItem> FamilyMetadataList { get; set; } = new();
    }
    private sealed class GoogleFontsMetadataItem
    {
        public string Family { get; set; } // font family name

        public string Category { get; set; } // Google category

        public List<string> Variants { get; set; } // e.g., ["regular","italic","700","700italic"]
    }
    #endregion
}
