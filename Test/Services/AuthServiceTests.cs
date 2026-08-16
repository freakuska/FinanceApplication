using System.IdentityModel.Tokens.Jwt;
using FinanceApp.Dbo.Models;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FinanceApp.Tests.Services;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ShouldReturnTokensAndUpdateLastLogin()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("USER", new[] { "operations.read" });
        var user = TestDataFactory.CreateUser("login@test.local", "correct-pass");
        context.AddRange(role, user, TestDataFactory.CreateUserRole(user.Id, role.Id));
        await context.SaveChangesAsync();

        var userService = new Mock<IUserService>();
        userService.Setup(x => x.UpdateLastLoginAsync(user.Id)).Returns(Task.CompletedTask);
        userService.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Login = user.Login,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            IsActive = true,
            IsVerified = false,
            CreatedAt = user.CreatedAt,
            Settings = new UserSettingsDto(),
            Roles = new List<RoleDto>()
        });

        var service = new AuthService(context, BuildJwtConfig(), userService.Object);
        var result = await service.LoginAsync(new LoginRequestDto { Email = user.Email, Password = "correct-pass" });

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        userService.Verify(x => x.UpdateLastLoginAsync(user.Id), Times.Once);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        jwt.Claims.Should().Contain(c => c.Type.Contains("email", StringComparison.OrdinalIgnoreCase) && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Type.EndsWith("role") && c.Value == "USER");
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowUnauthorized_WhenPasswordInvalid()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("wrong-password@test.local", "correct-pass");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new AuthService(context, BuildJwtConfig(), Mock.Of<IUserService>());

        var action = () => service.LoginAsync(new LoginRequestDto
        {
            Email = user.Email,
            Password = "bad-pass"
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUserAndLogin()
    {
        await using var context = TestDbFactory.CreateContext();
        var userRole = TestDataFactory.CreateRole("USER", new[] { "operations.own.manage" }, isSystem: true);
        context.Roles.Add(userRole);
        await context.SaveChangesAsync();

        var userService = new UserService(context);
        var service = new AuthService(context, BuildJwtConfig(), userService);

        var response = await service.RegisterAsync(new RegisterRequestDto
        {
            Email = "register@test.local",
            Password = "register-pass",
            FullName = "Registered User",
            Phone = "+79990000000"
        });

        response.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.User.Email.Should().Be("register@test.local");
        context.Users.Any(u => u.Email == "register@test.local").Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrow_WhenEmailAlreadyExists()
    {
        await using var context = TestDbFactory.CreateContext();
        context.Users.Add(TestDataFactory.CreateUser("dup-register@test.local"));
        await context.SaveChangesAsync();
        var service = new AuthService(context, BuildJwtConfig(), Mock.Of<IUserService>());

        var action = () => service.RegisterAsync(new RegisterRequestDto
        {
            Email = "dup-register@test.local",
            Password = "x",
            FullName = "Name",
            Phone = "1"
        });

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldRevokeOldTokenAndIssueNewOne()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("USER");
        var user = TestDataFactory.CreateUser("refresh@test.local");
        var oldToken = TestDataFactory.CreateRefreshToken(user.Id, "old-token", DateTime.UtcNow.AddHours(1));
        context.AddRange(role, user, TestDataFactory.CreateUserRole(user.Id, role.Id), oldToken);
        await context.SaveChangesAsync();

        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Login = user.Login,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            IsActive = true,
            IsVerified = false,
            CreatedAt = user.CreatedAt,
            Settings = new UserSettingsDto(),
            Roles = new List<RoleDto>()
        });

        var service = new AuthService(context, BuildJwtConfig(), userService.Object);
        var response = await service.RefreshTokenAsync("old-token");

        response.RefreshToken.Should().NotBe("old-token");
        (await context.RefreshTokens.FindAsync(oldToken.Id))!.RevokedAt.Should().NotBeNull();
        context.RefreshTokens.Count().Should().Be(2);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("2025-01-01", false, false)]
    [InlineData(null, true, false)]
    [InlineData(null, false, true)]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenTokenInvalid(string? expiredAtIso, bool revoked, bool inactiveUser)
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("refresh-invalid@test.local", isActive: !inactiveUser);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var tokenValue = "candidate-token";
        context.RefreshTokens.Add(TestDataFactory.CreateRefreshToken(
            user.Id,
            tokenValue,
            expiredAtIso is null ? DateTime.UtcNow.AddHours(1) : DateTime.Parse(expiredAtIso).ToUniversalTime(),
            revoked ? DateTime.UtcNow : null));
        await context.SaveChangesAsync();
        var service = new AuthService(context, BuildJwtConfig(), Mock.Of<IUserService>());

        if (expiredAtIso is null && !revoked && !inactiveUser)
        {
            await service.RevokeTokenAsync(tokenValue);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshTokenAsync(tokenValue));
            return;
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshTokenAsync(tokenValue));
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldReturnFalse_WhenTokenMissing()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new AuthService(context, BuildJwtConfig(), Mock.Of<IUserService>());

        var result = await service.RevokeTokenAsync("absent");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowUnauthorized_WhenUserInactive()
    {
        await using var context = TestDbFactory.CreateContext();
        var inactiveUser = TestDataFactory.CreateUser("inactive@test.local", "pass", isActive: false);
        context.Users.Add(inactiveUser);
        await context.SaveChangesAsync();
        var service = new AuthService(context, BuildJwtConfig(), Mock.Of<IUserService>());

        var action = () => service.LoginAsync(new LoginRequestDto
        {
            Email = inactiveUser.Email,
            Password = "pass"
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenTokenNotFound()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new AuthService(context, BuildJwtConfig(), Mock.Of<IUserService>());

        var action = () => service.RefreshTokenAsync("missing-token");

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldReturnTrue_WhenTokenExists()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("revoke@test.local");
        var refreshToken = TestDataFactory.CreateRefreshToken(user.Id, "revoke-me", DateTime.UtcNow.AddDays(1));
        context.AddRange(user, refreshToken);
        await context.SaveChangesAsync();
        var service = new AuthService(context, BuildJwtConfig(), Mock.Of<IUserService>());

        var result = await service.RevokeTokenAsync("revoke-me");

        result.Should().BeTrue();
        (await context.RefreshTokens.SingleAsync(x => x.Token == "revoke-me")).RevokedAt.Should().NotBeNull();
    }

    private static IConfiguration BuildJwtConfig()
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "super-secret-key-super-secret-key-123456",
            ["Jwt:Issuer"] = "FinanceApp.Tests",
            ["Jwt:Audience"] = "FinanceApp.Tests.Client",
            ["Jwt:AccessTokenLifetimeMinutes"] = "30",
            ["Jwt:RefreshTokenLifetimeDays"] = "7"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
