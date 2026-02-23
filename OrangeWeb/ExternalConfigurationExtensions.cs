
namespace OP_Shared_Library.Configurations
{
    /// <summary>
    /// Loads appsettings from an external folder (outside the project/output).
    /// Useful for multi-company deployments where config must not be shipped with each update.
    /// </summary>
    public static class ExternalConfigurationExtensions
    {
        /// <summary>
        /// Adds external appsettings.json + appsettings.{ENV}.json to the configuration pipeline.
        /// ENV = builder.Environment.EnvironmentName (e.g. "lp_development").
        ///
        /// Folder resolution priority:
        /// 1) OP_CONFIG_DIR                       -> explicit full path to config folder
        /// 2) OP_CONFIG_ROOT + OP_COMPANY         -> folder = {root}\{company}
        ///
        /// Expected files in the resolved folder:
        /// - appsettings.json
        /// - appsettings.{ENV}.json
        ///
        /// Note: Environment variables are added last so they override JSON settings (good for secrets).
        /// </summary>
        public static WebApplicationBuilder AddExternalAppSettings(this WebApplicationBuilder builder)
        {
            // Resolves the external configuration folder path.
            // Prefer OP_CONFIG_DIR if set, otherwise build it from OP_CONFIG_ROOT + OP_COMPANY.
            static string? ResolveConfigDir()
            {
                // 1) Direct explicit folder
                var direct = Environment.GetEnvironmentVariable("OP_CONFIG_DIR");
                if (!string.IsNullOrWhiteSpace(direct))
                {
                    return direct;
                }

                // 2) Root + company folder
                var root = Environment.GetEnvironmentVariable("OP_CONFIG_ROOT");
                var company = Environment.GetEnvironmentVariable("OP_COMPANY");

                if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(company))
                {
                    return Path.Combine(root, company);
                }

                // 3) No external folder configured
                return null;
            }

            // The ASP.NET Core environment name (e.g. "Development", "Production", or your custom "lp_development")
            var envName = builder.Environment.EnvironmentName;

            // Resolve external config folder (if any)
            var configDir = ResolveConfigDir();

            // If a config directory is configured, load JSON files from there
            if (!string.IsNullOrWhiteSpace(configDir))
            {
                // Fail fast if the folder path is wrong (helps catch misconfigured IIS/launchSettings)
                if (!Directory.Exists(configDir))
                {
                    throw new DirectoryNotFoundException($"OP config folder not found: '{configDir}'");
                }

                // Base + environment-specific config
                var baseFile = Path.Combine(configDir, "appsettings.json");
                var envFile = Path.Combine(configDir, $"appsettings.{envName}.json");

                // Add external JSON files to configuration.
                // optional:true -> app can still start even if a file is missing (you can change to false if you want strict mode)
                // reloadOnChange:true -> changes on disk can be picked up at runtime for some config patterns
                builder.Configuration
                    .AddJsonFile(baseFile, optional: true, reloadOnChange: true)
                    .AddJsonFile(envFile, optional: true, reloadOnChange: true);
            }

            // Add environment variables last so they override JSON (recommended for secrets / IIS web.config)
            builder.Configuration.AddEnvironmentVariables();

            return builder;
        }
    }
}