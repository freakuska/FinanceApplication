using FinanceApp.Dbo.Models;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Tests.Services;

public class RoleServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldPersistRoleAndUppercaseCode()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new RoleService(context);

        var result = await service.CreateAsync(new CreateRoleDto
        {
            Name = "Manager",
            Code = "manager",
            Description = "desc",
            Permissions = new List<string> { "reports.view" }
        });

        result.Code.Should().Be("MANAGER");
        result.Permissions.Should().Contain("reports.view");
        (await context.Roles.SingleAsync(x => x.Id == result.Id)).Code.Should().Be("MANAGER");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenCodeAlreadyExists()
    {
        await using var context = TestDbFactory.CreateContext();
        context.Roles.Add(TestDataFactory.CreateRole("ADMIN"));
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var action = () => service.CreateAsync(new CreateRoleDto
        {
            Name = "Another",
            Code = "ADMIN",
            Description = "dup",
            Permissions = new List<string>()
        });

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenRoleIsSystem()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("SYSTEM", isSystem: true);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var action = () => service.UpdateAsync(role.Id, new UpdateRoleDto { Name = "New name" });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*system role*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenRoleIsSystem()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("ADMIN", isSystem: true);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var result = await service.DeleteAsync(role.Id);

        result.Should().BeFalse();
        (await context.Roles.FindAsync(role.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnTrue_ForWildcardPermission()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("perm@test.local");
        var role = TestDataFactory.CreateRole("SUPER_ADMIN", new[] { "*" });
        var userRole = TestDataFactory.CreateUserRole(user.Id, role.Id);
        context.AddRange(user, role, userRole);
        await context.SaveChangesAsync();

        var service = new RoleService(context);
        var result = await service.HasPermissionAsync(user.Id, "users.manage");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ShouldReturnDistinctPermissions()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("permissions@test.local");
        var role1 = TestDataFactory.CreateRole("R1", new[] { "a", "b" });
        var role2 = TestDataFactory.CreateRole("R2", new[] { "b", "c" });
        context.AddRange(
            user,
            role1,
            role2,
            TestDataFactory.CreateUserRole(user.Id, role1.Id),
            TestDataFactory.CreateUserRole(user.Id, role2.Id));
        await context.SaveChangesAsync();

        var service = new RoleService(context);
        var permissions = await service.GetUserPermissionsAsync(user.Id);

        permissions.Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public async Task GetByIdAndGetByCode_ShouldReturnNull_WhenRoleMissing()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new RoleService(context);

        (await service.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
        (await service.GetByCodeAsync("MISSING")).Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAndGetByCode_ShouldReturnRole_WhenExists()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("AUDITOR");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var byId = await service.GetByIdAsync(role.Id);
        var byCode = await service.GetByCodeAsync("AUDITOR");

        byId!.Code.Should().Be("AUDITOR");
        byCode!.Id.Should().Be(role.Id);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnCreatedRoles()
    {
        await using var context = TestDbFactory.CreateContext();
        context.Roles.AddRange(TestDataFactory.CreateRole("R1"), TestDataFactory.CreateRole("R2"));
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var roles = await service.GetAllAsync();

        roles.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateRole_WhenRoleIsEditable()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("EDITOR", new[] { "old" }, isSystem: false);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var updated = await service.UpdateAsync(role.Id, new UpdateRoleDto
        {
            Name = "Editor Updated",
            Description = "new desc",
            Permissions = new List<string> { "new.permission" }
        });

        updated.Name.Should().Be("Editor Updated");
        updated.Permissions.Should().ContainSingle("new.permission");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenRoleMissing()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new RoleService(context);

        var action = () => service.UpdateAsync(Guid.NewGuid(), new UpdateRoleDto { Name = "x" });

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteRole_WhenRoleIsEditable()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("TEMP", isSystem: false);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var result = await service.DeleteAsync(role.Id);

        result.Should().BeTrue();
        (await context.Roles.FindAsync(role.Id)).Should().BeNull();
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnFalse_WhenPermissionAbsent()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("no-perm@test.local");
        var role = TestDataFactory.CreateRole("USER", new[] { "read" });
        context.AddRange(user, role, TestDataFactory.CreateUserRole(user.Id, role.Id));
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var hasPermission = await service.HasPermissionAsync(user.Id, "write");

        hasPermission.Should().BeFalse();
    }
}
