using System.Security.Claims;

namespace WarcraftArchive.Api.Helpers;

public static class HttpContextHelper
{
    public static Guid? GetUserId(this HttpContext context)
    {
        var claim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public static bool IsAdmin(this HttpContext context) =>
        context.User?.IsInRole("Admin") ?? false;
}
