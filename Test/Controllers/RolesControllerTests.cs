using FinanceApp.Api.Controllers;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceApp.Tests.Controllers;

public class RolesControllerTests
{
    private readonly Mock<IRoleService> _service = new();

    [Fact]
    public async Task GetAll_ShouldReturnOk()
    {
        var controller = new RolesController(_service.Object);
        _service.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<RoleDto>());

        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenRoleMissing()
    {
        var controller = new RolesController(_service.Object);
        _service.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((RoleDto)null!);

        var result = await controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetByIdAndGetByCode_ShouldReturnOk_WhenRoleFound()
    {
        var controller = new RolesController(_service.Object);
        var role = new RoleDto
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Code = "ADMIN",
            Description = "desc",
            Permissions = new List<string>(),
            IsSystem = true
        };
        _service.Setup(x => x.GetByIdAsync(role.Id)).ReturnsAsync(role);
        _service.Setup(x => x.GetByCodeAsync("ADMIN")).ReturnsAsync(role);

        (await controller.GetById(role.Id)).Should().BeOfType<OkObjectResult>();
        (await controller.GetByCode("admin")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetByCode_ShouldReturnNotFound_WhenRoleMissing()
    {
        var controller = new RolesController(_service.Object);
        _service.Setup(x => x.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((RoleDto)null!);

        var result = await controller.GetByCode("unknown");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAt_WhenSuccess()
    {
        var controller = new RolesController(_service.Object);
        var created = new RoleDto
        {
            Id = Guid.NewGuid(),
            Name = "User",
            Code = "USER",
            Description = "desc",
            Permissions = new List<string>(),
            IsSystem = false
        };
        _service.Setup(x => x.CreateAsync(It.IsAny<CreateRoleDto>())).ReturnsAsync(created);

        var result = await controller.Create(new CreateRoleDto
        {
            Name = "User",
            Code = "USER",
            Description = "desc",
            Permissions = new List<string>()
        });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenDuplicate()
    {
        var controller = new RolesController(_service.Object);
        _service.Setup(x => x.CreateAsync(It.IsAny<CreateRoleDto>())).ThrowsAsync(new InvalidOperationException("duplicate"));

        var result = await controller.Create(new CreateRoleDto
        {
            Name = "Role",
            Code = "ROLE",
            Description = "desc",
            Permissions = new List<string>()
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_ShouldMapBusinessExceptions()
    {
        var controller = new RolesController(_service.Object);
        var id = Guid.NewGuid();
        _service.Setup(x => x.UpdateAsync(id, It.IsAny<UpdateRoleDto>())).ThrowsAsync(new KeyNotFoundException("missing"));

        var missingResult = await controller.Update(id, new UpdateRoleDto());
        missingResult.Should().BeOfType<NotFoundObjectResult>();

        _service.Setup(x => x.UpdateAsync(id, It.IsAny<UpdateRoleDto>())).ThrowsAsync(new InvalidOperationException("system"));
        var invalidResult = await controller.Update(id, new UpdateRoleDto());
        invalidResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Delete_ShouldReturnBadRequest_WhenDeleteRejected()
    {
        var controller = new RolesController(_service.Object);
        _service.Setup(x => x.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var result = await controller.Delete(Guid.NewGuid());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent_WhenDeleted()
    {
        var controller = new RolesController(_service.Object);
        _service.Setup(x => x.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var result = await controller.Delete(Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task HasPermission_ShouldReturnOkEnvelope()
    {
        var controller = new RolesController(_service.Object);
        _service.Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), "reports.view")).ReturnsAsync(true);
        var userId = Guid.NewGuid();

        var result = await controller.HasPermission(userId, "reports.view");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserPermissions_ShouldReturnOkEnvelope()
    {
        var controller = new RolesController(_service.Object);
        _service.Setup(x => x.GetUserPermissionsAsync(It.IsAny<Guid>())).ReturnsAsync(new List<string> { "a", "b" });

        var result = await controller.GetUserPermissions(Guid.NewGuid());

        result.Should().BeOfType<OkObjectResult>();
    }
}
