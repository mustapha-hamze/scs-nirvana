using Microsoft.AspNetCore.Http;

namespace Web.Services.Tenancy;

// Backs the Application-facing tenant-selection port with the caller's ASP.NET Core session, so
// a selection made in one browser session never affects another session for the same account.
public sealed class SessionCurrentApplicationContext : ICurrentApplicationContext
{
    private const string SessionKey = "CurrentApplicationId";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionCurrentApplicationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? CurrentApplicationId
    {
        get => _httpContextAccessor.HttpContext?.Session.GetInt32(SessionKey);
        set
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null)
                return;

            if (value is int applicationId)
                session.SetInt32(SessionKey, applicationId);
            else
                session.Remove(SessionKey);
        }
    }
}
