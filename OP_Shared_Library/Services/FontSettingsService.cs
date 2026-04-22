using Microsoft.Extensions.Hosting;
using OP_Shared_Library.Struct;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OP_Shared_Library.Services;

// Service that loads/saves font settings (YAML) and generates a CSS file with the selected fonts.
public class FontSettingsService
{
    #region Dependency Injection
    // Hosting environment gives us ContentRootPath to resolve absolute file paths (yaml)
    private readonly IHostEnvironment _environment;
    private readonly ICustomLogger _customLogger;
    #endregion

    #region Private Properties
    // Semaphore to ensure thread-safe load/save operations (prevents concurrent writes).
    // One thread is inside and others waiting in queue (its here for read and write to yaml on disc)
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string _settingsYamlFilePath;
    private string _cssFilePath;
    private FontSettings _settings = new();
    private bool _isLoaded = false;
    #endregion

    #region Ctor
    public FontSettingsService(IHostEnvironment environment,
                               ICustomLogger customLogger)
    {
        _environment = environment;
        _customLogger = customLogger;

        FillProperties();
    }
    #endregion

    #region Events
    // Event fired whenever settings are successfully saved.
    public event Action OnFontSettingsChange;
    #endregion

    #region Public Methods
    // Returns a copy of the current settings, loading from disk on first call (lazy-load).
    public async Task<FontSettings> GetSettingsAsync()
    {
        if (_isLoaded)
        {
            // If we have data in memory, return only copy (prevent external changes)
            return Clone(_settings);
        }

        // SemaphoreSlim allow access to only 1 thread
        await _lock.WaitAsync();
        try
        {
            // Lazy load - if is not loaded yet (first load) load settings only once (not every time by calling)
            if (!_isLoaded)
            {
                // Load settings from disk (or create defaults) only once, we dont want many readings from other threads at 1 time
                _settings = await LoadFromDiskAsync();
                _isLoaded = true;
            }

            return Clone(_settings);
        }
        finally
        {
            // When some exception, release
            _lock.Release();
        }
    }

    // Validates and save new settings; also regenerates the CSS file.
    public async Task SaveAsync(FontSettings settings)
    {
        if (settings is null)
        {
            _customLogger.MyLogger.Error($"FontSettingsService: SaveAsync: Font Setting are NULL: {nameof(settings)}");
            throw new ArgumentException($"FontSettingsService: SaveAsync: Font Setting are NULL: {nameof(settings)}");
        }

        // Normalize whitespace and ensure required fields are valid.
        var sanitized = Sanitize(settings);
        Validate(sanitized);

        await _lock.WaitAsync();
        try
        {
            // Update cache and mark as loaded.
            _settings = sanitized;
            _isLoaded = true;

            // Persist YAML and regenerate CSS atomically within the lock.
            await WriteYamlAsync(_settings);
            await WriteCssAsync(_settings);
        }
        finally
        {
            _lock.Release();
        }

        // Notify subscribers that settings changed.
        OnFontSettingsChange?.Invoke();
    }
    #endregion

    #region Private Methods
    private void FillProperties()
    {
        _settingsYamlFilePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "data", "font-settings.yaml");
        _cssFilePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "css", "CustomFont.css");
    }

    // Loads settings from YAML; if missing or invalid, write defaults and CSS.
    private async Task<FontSettings> LoadFromDiskAsync()
    {
        // If yaml doesnt exist...
        if (!File.Exists(_settingsYamlFilePath))
        {
            // Create defaults and write both YAML and CSS.
            var defaults = new FontSettings();

            // Write*** can create file too
            await WriteYamlAsync(defaults);
            await WriteCssAsync(defaults);

            return defaults;
        }

        // If exists yaml file, continue...
        try
        {
            // Read YAML from disk and deserialize
            await using var stream = File.OpenRead(_settingsYamlFilePath);
            using var reader = new StreamReader(stream);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance) // Beacuase of FontFamily, GoogleFontUrl etc. in YAML
                .IgnoreUnmatchedProperties() // Say to YAML: When found key, which doesnt exists in YAML, just continue
                .Build();

            // Load from reader to FontSettings
            var loaded = deserializer.Deserialize<FontSettings>(reader) ?? new FontSettings();

            // Apply defaults if any key values are missing.
            if (string.IsNullOrWhiteSpace(loaded.FontFamily))
            {
                loaded.FontFamily = "Arimo";
            }

            if (string.IsNullOrWhiteSpace(loaded.GoogleFontUrl))
            {
                loaded.GoogleFontUrl = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(loaded.FallbackFontFamily))
            {
                loaded.FallbackFontFamily = "sans-serif";
            }

            // If CSS was deleted externally, regenerate it based on loaded settings.
            if (!File.Exists(_cssFilePath))
            {
                await WriteCssAsync(loaded);
            }

            return loaded;
        }
        catch
        {
            // On any parse/read error: reset to defaults and keep the app running.
            var defaults = new FontSettings();
            await WriteYamlAsync(defaults);
            await WriteCssAsync(defaults);
            return defaults;
        }
    }

    // Creates a copy of FontSettings
    private static FontSettings Clone(FontSettings source)
    {
        return new FontSettings
        {
            FontFamily = source.FontFamily,
            GoogleFontUrl = source.GoogleFontUrl,
            FallbackFontFamily = source.FallbackFontFamily
        };
    }

    // Trims whitespace and ensures string properties are non-null.
    private static FontSettings Sanitize(FontSettings settings)
    {
        return new FontSettings
        {
            FontFamily = settings.FontFamily?.Trim() ?? string.Empty,
            GoogleFontUrl = settings.GoogleFontUrl?.Trim() ?? string.Empty,
            FallbackFontFamily = settings.FallbackFontFamily?.Trim() ?? string.Empty
        };
    }

    // Validates the settings and normalizes the Google Fonts URL to a canonical, safe form when one is configured.
    private void Validate(FontSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.FontFamily))
        {
            _customLogger.MyLogger.Error($"FontSettingsService: Validate: Zadejte prosím název fontu.");
            throw new ArgumentException("Zadejte prosím název fontu.");
        }

        if (!string.IsNullOrWhiteSpace(settings.GoogleFontUrl))
        {
            // Ensure URL is absolute and points to fonts.googleapis.com.
            if (!Uri.TryCreate(settings.GoogleFontUrl, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Host, "fonts.googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                _customLogger.MyLogger.Error($"FontSettingsService: Validate: URL musí směřovat na doménu fonts.googleapis.com.");
                throw new ArgumentException("URL musí směřovat na doménu fonts.googleapis.com.");
            }

            // Check HTTPS.
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                _customLogger.MyLogger.Error($"FontSettingsService: Validate: Použijte prosím zabezpečenou HTTPS adresu Google Fonts.");
                throw new ArgumentException("Použijte prosím zabezpečenou HTTPS adresu Google Fonts.");
            }

            // Enforce modern css2 endpoint format.
            if (!uri.AbsolutePath.StartsWith("/css2", StringComparison.OrdinalIgnoreCase))
            {
                _customLogger.MyLogger.Error($"FontSettingsService: Validate: Očekávám Google Fonts URL ve formátu https://fonts.googleapis.com/css2?... .");
                throw new ArgumentException("Očekávám Google Fonts URL ve formátu https://fonts.googleapis.com/css2?... .");
            }

            // Normalize the URL by keeping only scheme, host, path, and query (drops fragments, userinfo, etc.).
            settings.GoogleFontUrl = GetStandardizeUrl(uri);
        }

        // Default fallback family if missing.
        if (string.IsNullOrWhiteSpace(settings.FallbackFontFamily))
        {
            settings.FallbackFontFamily = "sans-serif";
        }
    }

    // UriComponents.SchemeAndServer: get schema://host[:port] -> https://fonts.googleapis.com
    // UriComponents.PathAndQuery: add path and query -> /css2?family=Arimo&display=swap -> whatever after # will be removed
    // UriFormat.UriEscaped: standardize small, big letters -> remove ports -> remove # etc.
    //settings.GoogleFontUrl = uri.GetComponents(
    //    UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
    //    UriFormat.UriEscaped);
    private string GetStandardizeUrl(Uri uri)
    {
        if (uri is null) return string.Empty;

        return uri.GetComponents(
          UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
          UriFormat.UriEscaped);
    }

    // Serializes settings to YAML on disk (creates directory if needed).
    private async Task WriteYamlAsync(FontSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsYamlFilePath)!);

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            // Properties with NULL or DEFAULT value not write to YAML 
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitNull)
            .Build();

        var yaml = serializer.Serialize(settings);
        await File.WriteAllTextAsync(_settingsYamlFilePath, yaml, Encoding.UTF8);
    }

    // Generates the CSS content from settings and writes it to disk.
    private async Task WriteCssAsync(FontSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cssFilePath)!);
        var css = BuildCss(settings);
        await File.WriteAllTextAsync(_cssFilePath, css, Encoding.UTF8);
    }

    // Builds a CSS string that imports Google Fonts and sets a CSS variable with the font stack.
    private static string BuildCss(FontSettings settings)
    {
        var sb = new StringBuilder();

        // Optional @import for the Google Fonts URL (if provided).
        if (!string.IsNullOrWhiteSpace(settings.GoogleFontUrl))
        {
            sb.Append("@import url('");
            sb.Append(settings.GoogleFontUrl);
            sb.AppendLine("');");
            sb.AppendLine();
        }

        // Build a full font-family with fallback
        var fontValues = new List<string>();
        AddFontParts(settings.FontFamily, fontValues, wrapWithQuotes: true);
        AddFontParts(settings.FallbackFontFamily, fontValues, wrapWithQuotes: false);

        // Final fallback if nothing was set.
        if (fontValues.Count == 0)
        {
            fontValues.Add("'Arimo'");
            fontValues.Add("sans-serif");
        }

        // Expose the stack via a CSS custom property for easy reuse in styles.
        sb.AppendLine(":root {");
        sb.Append("    --MainFont: ");
        sb.Append(string.Join(", ", fontValues));
        sb.AppendLine(";");
        sb.AppendLine("}");
        sb.AppendLine();

        return sb.ToString();
    }

    // Splits a comma-separated list of font families, cleans quotes, and appends to fontValues list.
    private static void AddFontParts(string value, List<string> fontValues, bool wrapWithQuotes)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            // Remove any surrounding quotes and re-apply if requested.
            var cleaned = part.Trim().Trim('\'', '"');
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            fontValues.Add(wrapWithQuotes ? $"'{cleaned}'" : cleaned);
        }
    }
    #endregion
}
