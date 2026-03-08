namespace OP_Shared_Library.Configurations
{
    /// <summary>
    /// Loads appsettings from an external folder (outside the project/output).
    /// Useful for multi-company deployments where config must not be shipped with each update.
    /// </summary>
    public static class ExternalConfigurationExtensions
    {
        /// <summary>
        /// Resolves external config folder path from environment variables.
        /// 1) OP_CONFIG_DIR
        /// 2) OP_CONFIG_ROOT + OP_COMPANY
        /// </summary>
        public static string? ResolveConfigDir()
        {
            var direct = Environment.GetEnvironmentVariable("OP_CONFIG_DIR");
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            var root = Environment.GetEnvironmentVariable("OP_CONFIG_ROOT");
            var company = Environment.GetEnvironmentVariable("OP_COMPANY");

            if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(company))
            {
                return Path.Combine(root, company);
            }

            return null;
        }

        /// <summary>
        /// Adds external appsettings.json + appsettings.{ENV}.json to the configuration pipeline.
        /// ENV = builder.Environment.EnvironmentName (e.g. "lp_development").
        /// </summary>
        public static WebApplicationBuilder AddExternalAppSettings(this WebApplicationBuilder builder)
        {
            var envName = builder.Environment.EnvironmentName;
            var configDir = ResolveConfigDir();

            if (!string.IsNullOrWhiteSpace(configDir))
            {
                if (!Directory.Exists(configDir))
                {
                    throw new DirectoryNotFoundException($"OP config folder not found: '{configDir}'");
                }

                var baseFile = Path.Combine(configDir, "appsettings.json");
                var envFile = Path.Combine(configDir, $"appsettings.{envName}.json");

                builder.Configuration
                    .AddJsonFile(baseFile, optional: true, reloadOnChange: true)
                    .AddJsonFile(envFile, optional: true, reloadOnChange: true);
            }

            builder.Configuration.AddEnvironmentVariables();

            return builder;
        }

        // Helper: builds a safe absolute path inside configDir and blocks path traversal (../)
        private static string? TryBuildSafePath(string rootDir, string relativePath)
        {
            var candidatePath = Path.GetFullPath(Path.Combine(rootDir, relativePath));
            var rootPath = Path.GetFullPath(rootDir);

            return candidatePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) ? candidatePath : null;
        }


        private static IResult ResolveBrandingAsset(string relativePath, string contentType, string fallbackWebRootFileName, string? webRootPath)
        {
            var configDir = ResolveConfigDir();
            if (!string.IsNullOrWhiteSpace(configDir) && Directory.Exists(configDir))
            {
                var faviconRoot = Path.Combine(configDir, "Favicon");
                var candidatePath = TryBuildSafePath(faviconRoot, relativePath);
                if (candidatePath is not null && File.Exists(candidatePath))
                {
                    return Results.File(candidatePath, contentType);
                }
            }

            if (!string.IsNullOrWhiteSpace(webRootPath))
            {
                var fallbackPath = Path.Combine(webRootPath, fallbackWebRootFileName);
                if (File.Exists(fallbackPath))
                {
                    return Results.File(fallbackPath, contentType);
                }
            }

            return Results.NotFound();
        }

        // Function for theme mapping
        public static WebApplication ResolveMapping(this WebApplication app)
        {
            // Register endpoint for GET requests (we need to read themes from external folder)
            // for example: GET https://my-domain.cz/_branding/theme.css
            app.MapGet("/_branding/theme.css", (Microsoft.Extensions.Options.IOptions<CompanyBrandingOptions> brandingOptions) =>
            {
                // Find folder, where we have config (we have here also whatever_theme.css file too)
                var configDir = ExternalConfigurationExtensions.ResolveConfigDir();
                if (string.IsNullOrWhiteSpace(configDir) || !Directory.Exists(configDir))
                {
                    // If folder doesnt exists, we cant continue
                    return Results.NotFound();
                }

                // Read value from branding (ThemeCssFile item in appsettings.json)
                var configuredTheme = brandingOptions.Value.ThemeCssFile;

                // If nothing is set, use default theme file name in config folder (orange-tehem is always there)
                var themeFileName = string.IsNullOrWhiteSpace(configuredTheme) ? "orange-theme.css" : configuredTheme.Trim();

                // Security: if config points to a public URL/path (~/, /, or absolute URL),
                // we DO NOT treat it as an external file in configDir.
                // This endpoint is meant only to read a file from external folder.
                if (themeFileName.StartsWith("~/", StringComparison.Ordinal) ||
                    themeFileName.StartsWith("/", StringComparison.Ordinal) ||
                    Uri.IsWellFormedUriString(themeFileName, UriKind.Absolute))
                {
                    return Results.BadRequest("Branding.ThemeCssFile points to a public URL, not an external file.");
                }



                // 1) try requested theme file
                var candidatePath = TryBuildSafePath(configDir, themeFileName);
                if (candidatePath is null)
                {
                    return Results.BadRequest("Invalid theme file path.");
                }

                // 2) if requested file does not exist, fallback to orange-theme.css (it should always exist)
                if (!File.Exists(candidatePath))
                {
                    var fallbackPath = TryBuildSafePath(configDir, "orange-theme.css");
                    if (fallbackPath is null || !File.Exists(fallbackPath))
                    {
                        // Just in case (even though you said it always exists)
                        return Results.NotFound();
                    }

                    candidatePath = fallbackPath;
                }

                // Return CSS file content (UTF-8)
                return Results.File(candidatePath, "text/css; charset=utf-8");
            });

            app.MapGet("/_branding/favicon/favicon-96x96.png", (IWebHostEnvironment env) =>
                ResolveBrandingAsset("favicon-96x96.png", "image/png", "favicon-96x96.png", env.WebRootPath));

            app.MapGet("/_branding/favicon/favicon.svg", (IWebHostEnvironment env) =>
                ResolveBrandingAsset("favicon.svg", "image/svg+xml", "favicon.svg", env.WebRootPath));

            app.MapGet("/_branding/favicon/favicon.ico", (IWebHostEnvironment env) =>
                ResolveBrandingAsset("favicon.ico", "image/x-icon", "favicon.ico", env.WebRootPath));

            app.MapGet("/_branding/favicon/apple-touch-icon.png", (IWebHostEnvironment env) =>
                ResolveBrandingAsset("apple-touch-icon.png", "image/png", "apple-touch-icon.png", env.WebRootPath));

            return app;
        }
    }
}