using Pennywise.Models;
using Pennywise.Repositories;

namespace Pennywise.Services;

public static class AuthHelper
{
    public static User? GetCurrentUser(HttpContext context, UserRepository userRepo)
    {
        string? token = null;
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerVal = authHeader.ToString();
            if (headerVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = headerVal[7..].Trim();
            }
            else
            {
                token = headerVal.Trim();
            }
        }

        if (string.IsNullOrEmpty(token) && context.Request.Cookies.TryGetValue("pennywise_session", out var cookieToken))
        {
            token = cookieToken;
        }

        if (string.IsNullOrEmpty(token)) return null;

        var session = userRepo.GetSession(token);
        if (session == null) return null;

        return userRepo.GetUserById(session.UserId);
    }
}
