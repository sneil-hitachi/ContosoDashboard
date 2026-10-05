using System.Security.Claims;

namespace ContosoDashboard.Tests.TestInfrastructure;

public static class TestPrincipalFactory
{
    public static ClaimsPrincipal Create(
        int userId = 1,
        string name = "Test User",
        string email = "test@contoso.com",
        string role = "Employee",
        string? department = "Engineering")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };

        if (department is not null)
        {
            claims.Add(new Claim("Department", department));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}