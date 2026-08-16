using FinanceApp.Api.Controllers;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceApp.Tests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _service = new();

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenUserMissing()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((UserDto)null!);

        var result = await controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenServiceThrowsInvalidOperation()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.CreateAsync(It.IsAny<CreateUserDto>())).ThrowsAsync(new InvalidOperationException("duplicate"));

        var result = await controller.Create(new CreateUserDto
        {
            Email = "dup@test.local",
            Password = "1",
            FullName = "Name",
            Phone = "1"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenMissing()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserDto>())).ThrowsAsync(new KeyNotFoundException());

        var result = await controller.Update(Guid.NewGuid(), new UpdateUserDto());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenDeleteFailed()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var result = await controller.Delete(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnBadRequest_WhenServiceReturnsFalse()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.ChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<ChangePasswordDto>())).ReturnsAsync(false);

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "old",
            NewPassword = "new"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task VerifyEmail_ShouldReturnNotFound_WhenServiceReturnsFalse()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.VerifyEmailAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var result = await controller.VerifyEmail(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetAll_ShouldReturnOk()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.GetAllAsync(1, 50)).ReturnsAsync(new List<UserDto>());

        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AssignRole_ShouldReturnBadRequest_WhenServiceReturnsFalse()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.AssignRoleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(false);

        var result = await controller.AssignRole(new AssignRoleRequest
        {
            UserId = Guid.NewGuid(),
            RoleCode = "ADMIN",
            AssignedBy = Guid.NewGuid()
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RemoveRole_ShouldReturnBadRequest_WhenServiceReturnsFalse()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.RemoveRoleAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(false);

        var result = await controller.RemoveRole(new RemoveRoleRequest
        {
            UserId = Guid.NewGuid(),
            RoleCode = "ADMIN"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateLastLogin_ShouldInvokeServiceAndReturnOk()
    {
        var controller = new UsersController(_service.Object);
        var userId = Guid.NewGuid();

        var result = await controller.UpdateLastLogin(userId);

        result.Should().BeOfType<OkResult>();
        _service.Verify(x => x.UpdateLastLoginAsync(userId), Times.Once);
    }

    [Fact]
    public async Task GetByEmail_ShouldReturnOk_WhenUserFound()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.GetByEmailAsync("user@test.local")).ReturnsAsync(new UserDto
        {
            Id = Guid.NewGuid(),
            Login = "user@test.local",
            Email = "user@test.local",
            FullName = "User",
            Phone = "1",
            AvatarUrl = "",
            IsActive = true,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            Settings = new UserSettingsDto(),
            Roles = new List<RoleDto>()
        });

        var result = await controller.GetByEmail("user@test.local");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserRoles_ShouldReturnOk()
    {
        var controller = new UsersController(_service.Object);
        _service.Setup(x => x.GetUserRolesAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RoleDto>());

        var result = await controller.GetUserRoles(Guid.NewGuid());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SuccessPaths_ShouldReturnPositiveHttpCodes()
    {
        var controller = new UsersController(_service.Object);
        var userId = Guid.NewGuid();
        _service.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(new UserDto
        {
            Id = userId,
            Login = "user@test.local",
            Email = "user@test.local",
            FullName = "User",
            Phone = "1",
            AvatarUrl = "",
            IsActive = true,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            Settings = new UserSettingsDto(),
            Roles = new List<RoleDto>()
        });
        _service.Setup(x => x.CreateAsync(It.IsAny<CreateUserDto>())).ReturnsAsync(new UserDto
        {
            Id = userId,
            Login = "new@test.local",
            Email = "new@test.local",
            FullName = "New",
            Phone = "1",
            AvatarUrl = "",
            IsActive = true,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            Settings = new UserSettingsDto(),
            Roles = new List<RoleDto>()
        });
        _service.Setup(x => x.UpdateAsync(userId, It.IsAny<UpdateUserDto>())).ReturnsAsync(new UserDto
        {
            Id = userId,
            Login = "user@test.local",
            Email = "user@test.local",
            FullName = "Updated",
            Phone = "1",
            AvatarUrl = "",
            IsActive = true,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            Settings = new UserSettingsDto(),
            Roles = new List<RoleDto>()
        });
        _service.Setup(x => x.DeleteAsync(userId)).ReturnsAsync(true);
        _service.Setup(x => x.ChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<ChangePasswordDto>())).ReturnsAsync(true);
        _service.Setup(x => x.VerifyEmailAsync(userId)).ReturnsAsync(true);
        _service.Setup(x => x.AssignRoleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
        _service.Setup(x => x.RemoveRoleAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(true);

        (await controller.GetById(userId)).Should().BeOfType<OkObjectResult>();
        (await controller.Create(new CreateUserDto { Email = "new@test.local", Password = "1", FullName = "New", Phone = "1" })).Should().BeOfType<CreatedAtActionResult>();
        (await controller.Update(userId, new UpdateUserDto { FullName = "Updated" })).Should().BeOfType<OkObjectResult>();
        (await controller.Delete(userId)).Should().BeOfType<NoContentResult>();
        (await controller.ChangePassword(new ChangePasswordRequest { UserId = userId, CurrentPassword = "old", NewPassword = "new" })).Should().BeOfType<OkResult>();
        (await controller.VerifyEmail(userId)).Should().BeOfType<OkResult>();
        (await controller.AssignRole(new AssignRoleRequest { UserId = userId, RoleCode = "ADMIN", AssignedBy = userId })).Should().BeOfType<OkResult>();
        (await controller.RemoveRole(new RemoveRoleRequest { UserId = userId, RoleCode = "ADMIN" })).Should().BeOfType<OkResult>();
    }
}
