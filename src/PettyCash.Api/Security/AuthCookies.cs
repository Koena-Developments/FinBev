namespace PettyCash.Api.Security;

public static class AuthCookies
{
    public const string Name = "pc_session";
    public const int LifetimeHours = 8;

    public static CookieOptions Issue(HttpRequest request, bool isDevelopment)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment || request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(LifetimeHours)
        };
    }
}
