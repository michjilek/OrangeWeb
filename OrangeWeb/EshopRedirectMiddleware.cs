using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Op_LP;

public class EshopRedirectMiddleware
{
    private readonly RequestDelegate _next;

    public EshopRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Zkontroluj, zda URL obsahuje "/eshop"
        if (context.Request.Path.StartsWithSegments("/eshop"))
        {
            // Přesměrování na localhost:5001
            context.Response.Redirect("http://localhost:5001" + context.Request.Path + context.Request.QueryString, permanent: false);
            return;
        }

        // Pokračuje k dalšímu middleware
        await _next(context);
    }
}
