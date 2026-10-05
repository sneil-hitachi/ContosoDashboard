using System.Security.Claims;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public static class MockLoginClaims
{
    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Department))
        {
            claims.Add(new Claim("Department", user.Department));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "MockCookie"));
    }
}