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

    /// <summary>
    /// Effective access roles. Cognito groups arrive as one or more "cognito:groups" claims.
    /// When auth is disabled (local dev), the caller is treated as an Admin.
    /// </summary>
    public static IReadOnlyCollection<string> GetRoles(this HttpContext ctx, IConfiguration config)
    {
        if (config.GetValue<bool>("DisableAuth"))
            return new[] { Services.ModelAccess.Admin };

        return ctx.User.FindAll("cognito:groups").Select(c => c.Value).ToArray();
    }
}
