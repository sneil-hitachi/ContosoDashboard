using System.Security.Claims;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Authentication;

public sealed class MockLoginClaimsTests
{
    [Fact]
    public void PrincipalClaimsComeFromTheSelectedPersistedUser()
    {
        var user = new User
        {
            UserId = 12,
            DisplayName = "Morgan Lee",
            Email = "morgan@contoso.com",
            Role = UserRole.TeamLead,
            Department = "Engineering"
        };

        var principal = MockLoginClaims.CreatePrincipal(user);

        Assert.Equal("12", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Morgan Lee", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("morgan@contoso.com", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("TeamLead", principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal("Engineering", principal.FindFirstValue("Department"));
    }
}