using System.Security.Claims;
using FinanceApp.Api.Controllers;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceApp.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IUserService> _userService = new();

    [Fact]
    public async Task Login_ShouldReturnOkAndSetCookies()
    {
        var controller = CreateController();
        var now = DateTime.UtcNow;
        _authService.Setup(x => x.LoginAsync(It.IsAny<LoginRequestDto>())).ReturnsAsync(new AuthResponseDto
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = now.AddMinutes(30),
            User = BuildUserDto()
        });

        var result = await controller.Login(new LoginRequestDto { Email = "u@test.local", Password = "p" });

        result.Should().BeOfType<OkObjectResult>();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("AccessToken=access-token");
        controller.Response.Headers.SetCookie.ToString().Should().Contain("RefreshToken=refresh-token");
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenCredentialsInvalid()
    {
        var controller = CreateController();
        _authService.Setup(x => x.LoginAsync(It.IsAny<LoginRequestDto>())).ThrowsAsync(new UnauthorizedAccessException("bad credentials"));

        var result = await controller.Login(new LoginRequestDto { Email = "u@test.local", Password = "wrong" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Register_ShouldReturnCreatedAtAction()
    {
        var controller = CreateController();
        _authService.Setup(x => x.RegisterAsync(It.IsAny<RegisterRequestDto>())).ReturnsAsync(new AuthResponseDto
        {
            AccessToken = "new-access",
            RefreshToken = "new-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = BuildUserDto()
        });

        var result = await controller.Register(new RegisterRequestDto
        {
            Email = "new@test.local",
            Password = "pass",
            FullName = "New User",
            Phone = "+70000000000"
        });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenBusinessValidationFails()
    {
        var controller = CreateController();
        _authService.Setup(x => x.RegisterAsync(It.IsAny<RegisterRequestDto>()))
            .ThrowsAsync(new InvalidOperationException("already exists"));

        var result = await controller.Register(new RegisterRequestDto
        {
            Email = "dup@test.local",
            Password = "pass",
            FullName = "Dup",
            Phone = "1"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Refresh_ShouldReturnUnauthorized_WhenRefreshCookieMissing()
    {
        var controller = CreateController();

        var result = await controller.Refresh();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Refresh_ShouldReturnOk_WhenRefreshCookieProvided()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "RefreshToken=valid-token";

        var controller = CreateController(context);
        _authService.Setup(x => x.RefreshTokenAsync("valid-token")).ReturnsAsync(new AuthResponseDto
        {
            AccessToken = "new-access",
            RefreshToken = "new-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = BuildUserDto()
        });

        var result = await controller.Refresh();

        result.Should().BeOfType<OkObjectResult>();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("RefreshToken=new-refresh");
    }

    [Fact]
    public async Task Logout_ShouldRevokeToken_WhenCookieExists()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "RefreshToken=logout-token";
        var controller = CreateController(context);

        var result = await controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        _authService.Verify(x => x.RevokeTokenAsync("logout-token"), Times.Once);
    }

    [Fact]
    public async Task GetMe_ShouldReturnUnauthorized_WhenClaimInvalid()
    {
        var controller = CreateController(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        });

        var result = await controller.GetMe();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetMe_ShouldReturnNotFound_WhenUserMissing()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"))
        };
        var controller = CreateController(context);
        _userService.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((UserDto)null!);

        var result = await controller.GetMe();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMe_ShouldReturnOk_WhenUserFound()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"))
        };
        var controller = CreateController(context);
        _userService.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(BuildUserDto(userId));

        var result = await controller.GetMe();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<UserDto>();
    }

    private AuthController CreateController(DefaultHttpContext? context = null)
    {
        var controller = new AuthController(_authService.Object, _userService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context ?? new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static UserDto BuildUserDto(Guid? id = null)
    {
        return new UserDto
        {
            Id = id ?? Guid.NewGuid(),
            Login = "user",
            Email = "user@test.local",
            FullName = "User",
            Phone = "+70000000000",
            AvatarUrl = string.Empty,
            IsActive = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            Settings = new UserSettingsDto(),
            Roles = new List<RoleDto>()
        };
    }
}
