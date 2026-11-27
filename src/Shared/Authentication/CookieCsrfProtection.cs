using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PortalEmpleo.Shared.Authentication;

public static class CookieCsrfProtection
{
    public static bool IsAllowed(HttpRequest request, string expectedOrigin)
    {
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method) ||
            !AuthenticatedToken.UsesCookie(request))
            return true;

        var configuredOrigin = expectedOrigin.TrimEnd('/');
        var origin = request.Headers.Origin.ToString().TrimEnd('/');
        if (origin.Length != 0)
            return string.Equals(origin, configuredOrigin, StringComparison.OrdinalIgnoreCase);

        // Ausencia de Origin: solo aceptar si el propio navegador declara same-origin.
        return string.Equals(request.Headers["Sec-Fetch-Site"].ToString(),
            "same-origin", StringComparison.OrdinalIgnoreCase);
    }

    public static IApplicationBuilder UseCookieCsrfProtection(
        this IApplicationBuilder app, IConfiguration configuration)
    {
        var expectedOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        return app.Use(async (context, next) =>
        {
            if (!IsAllowed(context.Request, expectedOrigin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new { message = "Origen de solicitud no permitido." });
                return;
            }
            await next();
        });
    }
}
