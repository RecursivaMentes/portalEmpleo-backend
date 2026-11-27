using Application.Auth;
using Microsoft.AspNetCore.Http;
using PortalEmpleo.Shared.Authentication;
using Xunit;

namespace Usuarios.Tests;

public class SeguridadCookiesYContrasenasTests
{
    private const string Trusted = "http://localhost:5173";

    [Theory]
    [InlineData("", "cookie123", "cookie123")]
    [InlineData("Bearer explicit123", "cookie123", "explicit123")]
    [InlineData("Bearer explicit123", "", "explicit123")]
    public void Token_RespetaPrioridadBearer(string authorization, string cookie, string expected)
    {
        var context = new DefaultHttpContext();
        if (authorization.Length > 0) context.Request.Headers.Authorization = authorization;
        if (cookie.Length > 0) context.Request.Headers.Cookie = "AuthToken=" + cookie;
        Assert.Equal(expected, AuthenticatedToken.Read(context.Request));
    }

    [Fact]
    public void PostConCookieYOrigenAjeno_SeRechaza()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers.Cookie = "AuthToken=valid";
        context.Request.Headers.Origin = "https://example.invalid";
        Assert.False(CookieCsrfProtection.IsAllowed(context.Request, Trusted));
    }

    [Fact]
    public void PostConCookieYOrigenPermitido_SeAcepta()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers.Cookie = "AuthToken=valid";
        context.Request.Headers.Origin = Trusted;
        Assert.True(CookieCsrfProtection.IsAllowed(context.Request, Trusted));
    }

    [Fact]
    public void PostConCookieSinOrigenNiFetchSite_SeRechaza()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers.Cookie = "AuthToken=valid";
        Assert.False(CookieCsrfProtection.IsAllowed(context.Request, Trusted));
    }

    [Fact]
    public void PostConCookieSameOrigin_SeAcepta()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers.Cookie = "AuthToken=valid";
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        Assert.True(CookieCsrfProtection.IsAllowed(context.Request, Trusted));
    }

    [Fact]
    public void PostConBearerSinCookie_NoExigeOrigen()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers.Authorization = "Bearer valid";
        Assert.True(CookieCsrfProtection.IsAllowed(context.Request, Trusted));
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void LongitudContrasena_PoliticaUnica(int length, bool expected)
    {
        Assert.Equal(expected, PasswordPolicy.IsValid(new string('X', length)));
    }
}
