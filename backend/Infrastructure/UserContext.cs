using System.Security.Claims;

namespace Chatbot.Api.Infrastructure;

public static class UserContext
{
    /// <summary>Resolves the caller's user id from Cognito claims, or a dev fallback.</summary>
    public static string GetUserId(this HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue("sub")
            ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return sub ?? "dev-user";
    }
}
