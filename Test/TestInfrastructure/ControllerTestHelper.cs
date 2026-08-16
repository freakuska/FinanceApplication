using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Tests.TestInfrastructure;

internal static class ControllerTestHelper
{
    public static ControllerContext BuildControllerContext(Guid? userId = null, bool https = false)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            claims.Add(new Claim("userId", userId.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Email, "user@test.local"));
        }

        var identity = new ClaimsIdentity(claims, userId.HasValue ? "TestAuth" : null);
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext
        {
            User = principal,
            Request = { Scheme = https ? "https" : "http" }
        };

        return new ControllerContext
        {
            HttpContext = context
        };
    }
}
