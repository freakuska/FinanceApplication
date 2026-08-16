using System.Security.Claims;
using FinanceApp.Api.Extensions;
using FinanceApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceApp.Tests.Extensions;

public class ApiExtensionsTests
{
    [Fact]
    public void ClaimsPrincipalExtensions_ShouldReturnExpectedIdentityData()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "user@test.local"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "ADMIN"),
            new Claim("permission", "reports.view")
        }, "TestAuth"));

        principal.GetUserId().Should().Be(userId);
        principal.GetEmail().Should().Be("user@test.local");
        principal.GetFullName().Should().Be("Test User");
        principal.GetRoles().Should().ContainSingle("ADMIN");
        principal.HasRole("ADMIN").Should().BeTrue();
        principal.HasPermission("reports.view").Should().BeTrue();
        principal.HasPermission("users.manage").Should().BeFalse();
    }

    [Fact]
    public void ClaimsPrincipalExtensions_ShouldFallbackToUserIdClaim()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString())
        }, "TestAuth"));

        principal.GetUserId().Should().Be(userId);
    }

    [Fact]
    public void AddApplicationServices_ShouldRegisterAllCoreServicesAsScoped()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();

        services.Should().Contain(sd => sd.ServiceType == typeof(IUserService)
                                        && sd.ImplementationType == typeof(UserService)
                                        && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(IRoleService)
                                        && sd.ImplementationType == typeof(RoleService)
                                        && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(ITagService)
                                        && sd.ImplementationType == typeof(TagService)
                                        && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(IFinancialOperationService)
                                        && sd.ImplementationType == typeof(FinancialOperationService)
                                        && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(IReportService)
                                        && sd.ImplementationType == typeof(ReportService)
                                        && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(IAuthService)
                                        && sd.ImplementationType == typeof(AuthService)
                                        && sd.Lifetime == ServiceLifetime.Scoped);
    }
}
