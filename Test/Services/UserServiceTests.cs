using System.Text.Json;
using FinanceApp.Dbo.Models;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldCreateUserAndAssignDefaultRole()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserService(context);
        var dto = new CreateUserDto
        {
            Email = "new.user@test.local",
            Password = "secret123",
            FullName = "New User",
            Phone = "+79998887766"
        };

        var result = await service.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Email.Should().Be(dto.Email);
        result.IsActive.Should().BeTrue();
        result.Roles.Should().ContainSingle(r => r.Code == "USER");

        var createdUser = await context.Users.SingleAsync(u => u.Email == dto.Email);
        createdUser.PasswordHash.Should().NotBe(dto.Password);
        var roleCount = await context.UserRoles.CountAsync(ur => ur.UserId == createdUser.Id);
        roleCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenEmailAlreadyExists()
    {
        await using var context = TestDbFactory.CreateContext();
        context.Users.Add(TestDataFactory.CreateUser("duplicate@test.local"));
        await context.SaveChangesAsync();

        var service = new UserService(context);

        var action = () => service.CreateAsync(new CreateUserDto
        {
            Email = "duplicate@test.local",
            Password = "x",
            FullName = "Duplicate",
            Phone = "1"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateOnlyProvidedFields()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("update@test.local");
        user.FullName = "Old Name";
        user.Phone = "111";
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new UserService(context);
        var dto = new UpdateUserDto
        {
            FullName = "New Name",
            Settings = new UserSettingsDto { Currency = "USD", Language = "en", Timezone = "UTC+3" }
        };

        var result = await service.UpdateAsync(user.Id, dto);

        result.FullName.Should().Be("New Name");
        result.Phone.Should().Be("111");
        result.Settings.Currency.Should().Be("USD");

        var updated = await context.Users.SingleAsync(u => u.Id == user.Id);
        updated.FullName.Should().Be("New Name");
        JsonSerializer.Deserialize<UserSettingsDto>(updated.Settings)!.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteUser()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("delete.user@test.local");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new UserService(context);

        var result = await service.DeleteAsync(user.Id);

        result.Should().BeTrue();
        (await context.Users.FindAsync(user.Id))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldUpdateHash_WhenCurrentPasswordIsValid()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("pass@test.local", "old-pass");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var oldHash = user.PasswordHash;

        var service = new UserService(context);
        var result = await service.ChangePasswordAsync(user.Id, new ChangePasswordDto
        {
            CurrentPassword = "old-pass",
            NewPassword = "new-pass"
        });

        result.Should().BeTrue();
        (await context.Users.FindAsync(user.Id))!.PasswordHash.Should().NotBe(oldHash);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldThrow_WhenCurrentPasswordInvalid()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("wrong-pass@test.local", "old-pass");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new UserService(context);
        var action = () => service.ChangePasswordAsync(user.Id, new ChangePasswordDto
        {
            CurrentPassword = "bad-pass",
            NewPassword = "new-pass"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*incorrect*");
    }

    [Fact]
    public async Task AssignRoleAsync_ShouldReturnFalse_WhenRoleNotFound()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("assign@test.local");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new UserService(context);

        var result = await service.AssignRoleAsync(user.Id, "MISSING", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AssignRoleAsync_ShouldAddRole_WhenRoleExistsAndNotAssigned()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("assign2@test.local");
        var role = TestDataFactory.CreateRole("MANAGER");
        context.AddRange(user, role);
        await context.SaveChangesAsync();
        var service = new UserService(context);

        var result = await service.AssignRoleAsync(user.Id, "MANAGER", user.Id);

        result.Should().BeTrue();
        var assignedRoles = await context.UserRoles
            .Include(ur => ur.Role)
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Role.Code)
            .ToListAsync();
        assignedRoles.Should().Contain("MANAGER");
    }

    [Fact]
    public async Task RemoveRoleAsync_ShouldReturnFalse_WhenRoleNotAssigned()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("remove@test.local");
        var role = TestDataFactory.CreateRole("ADMIN");
        context.AddRange(user, role);
        await context.SaveChangesAsync();
        var service = new UserService(context);

        var result = await service.RemoveRoleAsync(user.Id, "ADMIN");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnRequestedPage()
    {
        await using var context = TestDbFactory.CreateContext();
        var users = Enumerable.Range(0, 5).Select(i => TestDataFactory.CreateUser($"user{i}@test.local")).ToList();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var service = new UserService(context);
        var page2 = await service.GetAllAsync(page: 2, pageSize: 2);

        page2.Should().HaveCount(2);
        page2.Select(x => x.Email).Should().NotContain("user0@test.local");
    }

    [Fact]
    public async Task VerifyEmailAsync_ShouldSetVerificationFlag()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("verify@test.local", isVerified: false);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new UserService(context);

        var result = await service.VerifyEmailAsync(user.Id);

        result.Should().BeTrue();
        (await context.Users.FindAsync(user.Id))!.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateLastLoginAsync_ShouldSetLastLoginDate()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("last-login@test.local");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new UserService(context);

        await service.UpdateLastLoginAsync(user.Id);

        (await context.Users.FindAsync(user.Id))!.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnUser_WhenExists()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("USER");
        var user = TestDataFactory.CreateUser("lookup@test.local");
        context.AddRange(role, user, TestDataFactory.CreateUserRole(user.Id, role.Id));
        await context.SaveChangesAsync();
        var service = new UserService(context);

        var result = await service.GetByEmailAsync(user.Email);

        result.Should().NotBeNull();
        result.Email.Should().Be("lookup@test.local");
        result.Roles.Should().ContainSingle(r => r.Code == "USER");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenUserMissing()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserService(context);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserRolesAsync_ShouldReturnMappedRoles()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("roles@test.local");
        var role1 = TestDataFactory.CreateRole("ADMIN");
        var role2 = TestDataFactory.CreateRole("MANAGER");
        context.AddRange(
            user,
            role1,
            role2,
            TestDataFactory.CreateUserRole(user.Id, role1.Id),
            TestDataFactory.CreateUserRole(user.Id, role2.Id));
        await context.SaveChangesAsync();
        var service = new UserService(context);

        var roles = await service.GetUserRolesAsync(user.Id);

        roles.Select(r => r.Code).Should().BeEquivalentTo(new[] { "ADMIN", "MANAGER" });
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenUserMissing()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserService(context);

        var action = () => service.UpdateAsync(Guid.NewGuid(), new UpdateUserDto { FullName = "Missing" });

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_AndVerifyEmailAsync_ShouldReturnFalse_WhenUserMissing()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserService(context);
        var userId = Guid.NewGuid();

        (await service.DeleteAsync(userId)).Should().BeFalse();
        (await service.VerifyEmailAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveRoleAsync_ShouldRemoveAssignedRole()
    {
        await using var context = TestDbFactory.CreateContext();
        var user = TestDataFactory.CreateUser("remove-success@test.local");
        var role = TestDataFactory.CreateRole("ADMIN");
        var userRole = TestDataFactory.CreateUserRole(user.Id, role.Id);
        context.AddRange(user, role, userRole);
        await context.SaveChangesAsync();
        var service = new UserService(context);

        var result = await service.RemoveRoleAsync(user.Id, "ADMIN");

        result.Should().BeTrue();
        (await context.UserRoles.AnyAsync(x => x.Id == userRole.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateLastLoginAsync_ShouldIgnoreMissingUser()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserService(context);

        var action = () => service.UpdateLastLoginAsync(Guid.NewGuid());

        await action.Should().NotThrowAsync();
    }
}
