using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using OP_Razor_Components_Library.Components.EditableImage.Services;
using OP_Razor_Components_Library.Components.News.Services;
using OP_Razor_Components_Library.Components.Photo_Gallery.Services;
using OP_Razor_Components_Library.Components.References.Services;
using Serilog;

namespace OP_Razor_Components_Library;
public class Installer
{
    public void Install(IServiceCollection services)
    {
        if (services is null) return;

        services.AddScoped<ServicesService>();
        services.AddScoped<EditNewsService>();
        services.AddScoped<EditReferencesService>();
        services.AddScoped<EditImageService>();
        services.AddScoped<EditPhotoGalleryService>();
        services.AddSingleton<IMinioClient>(sp =>
        {
            // Get Configuration from appsettings.json
            var cfg = sp.GetRequiredService<IConfiguration>().GetSection("Minio");
            return new MinioClient()
                .WithEndpoint(new Uri(cfg["Endpoint"] ?? "http://localhost:9000"))
                .WithCredentials(cfg["AccessKey"], cfg["SecretKey"])
                .Build();
        });
    }
}
