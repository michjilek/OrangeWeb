using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.StaticFiles;
using Minio;
using Op_LP.Services;
using Op_LP.Services.Admin;
using OP_Razor_Components_Library.Components.Actualities.Services;
using OP_Razor_Components_Library.Components.EditableImage.Services;
using OP_Razor_Components_Library.Components.Navigation.Services;
using OP_Razor_Components_Library.Components.News.Services;
using OP_Razor_Components_Library.Components.Photo_Gallery.Services;
using OP_Razor_Components_Library.Components.References.Services;
using OP_Shared_Library.Configurations;
using OP_Shared_Library.Interfaces;
using OP_Shared_Library.Notify;
using OP_Shared_Library.Notify.Interface;
using OP_Shared_Library.Services;

// Create Builder (Dependency Injection)
var builder = WebApplication.CreateBuilder(args);

// ? EXTERNAL APPSETTINGS (outside of project)
builder.AddExternalAppSettings();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        builder.Configuration.GetSection("CircuitOptions").Bind(options);
        if (builder.Environment.IsDevelopment())
        {
            options.DetailedErrors = true;
        }
    });

// Authentification
builder.Services.AddAuthentication("Identity.Application").AddCookie();
builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<IAdminCredentialValidator, AdminCredentialValidator>();

// Configuration
builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection("AdminAuth"));
builder.Services.Configure<ContactFormEmailOptions>(builder.Configuration.GetSection("ContactFormEmail"));
builder.Services.Configure<CompanyBrandingOptions>(builder.Configuration.GetSection("Branding"));

builder.Services.AddHttpClient();

// Services - singletons
builder.Services.AddSingleton<ICustomLogger, CustomLogger>();
builder.Services.AddSingleton<IMinIoService, MinIoService>();
builder.Services.AddSingleton<IYamlService, YamlService>();
builder.Services.AddScoped<NavigationService>();
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    // Get Configuration from appsettings.json (te� u� m��e b�t extern�)
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("Minio");
    var env = sp.GetRequiredService<IHostEnvironment>();

    var client = new MinioClient()
       .WithEndpoint(new Uri(cfg["Endpoint"]))
        .WithCredentials(cfg["AccessKey"], cfg["SecretKey"]);

    // In Local Development without SSL, other with SSL
    if (!env.IsDevelopment())
    {
        client = client.WithSSL();
    }

    return client.Build();
});
builder.Services.AddSingleton<GoogleFontsCatalogService>();

// Services - scoped (for every single instance of web is one instance)
builder.Services.AddScoped<INotifier, Notifier>();
builder.Services.AddScoped<TranslationsService>();
builder.Services.AddScoped<EditModeService>();
builder.Services.AddScoped<ServicesService>();
builder.Services.AddScoped<EditNewsService>();
builder.Services.AddScoped<EditActualitiesService>();
builder.Services.AddScoped<EditReferencesService>();
builder.Services.AddScoped<EditImageService>();
builder.Services.AddScoped<EditPhotoGalleryService>();
builder.Services.AddScoped<FontSettingsService>();
builder.Services.AddScoped<IContactFormSender, ContactFormEmailService>();

var app = builder.Build();

// Log App START
var customLogger = app.Services.GetRequiredService<ICustomLogger>();
customLogger.MyLogger.Information($"[ONLY SERVER APP]: START...");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = new FileExtensionContentTypeProvider
    {
        Mappings = { [".yaml"] = "application/x-yaml" }
    },

    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";

        var isFingerprinted =
               path.Contains(".bundle.scp.", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase);

        if (isFingerprinted)
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
            return;
        }

        if (path.EndsWith("/app-version.json", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-store, must-revalidate";
            return;
        }

        if (path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=2592000"; // 30 days
            return;
        }

        if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable"; // 1 y
        }
    }
});

// Custom function for theme mapping from external folder
app.ResolveMapping();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapBlazorHub();
app.MapRazorPages();
app.MapControllers();
app.MapFallbackToPage("/_Host");

app.Run();
