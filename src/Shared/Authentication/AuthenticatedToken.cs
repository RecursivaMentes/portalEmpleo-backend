using Microsoft.AspNetCore.Http;

namespace PortalEmpleo.Shared.Authentication;

public static class AuthenticatedToken
{
    public static string? Read(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearer = authorization["Bearer ".Length..].Trim();
            return string.IsNullOrWhiteSpace(bearer) ? null : bearer;
        }

        return request.Cookies.TryGetValue("AuthToken", out var cookie)
            && !string.IsNullOrWhiteSpace(cookie) ? cookie : null;
    }

    public static bool UsesCookie(HttpRequest request)
    {
        return !request.Headers.Authorization.ToString()
                    .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
               && request.Cookies.ContainsKey("AuthToken");
    }
}