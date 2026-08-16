using FinanceApp.Dbo.Enums;
using FinanceApp.Infrastructure.Data;
using FinanceApp.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Tests.Infrastructure;

public class ApplicationDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_ShouldPopulateCreatedAndUpdatedAt_OnAdd()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("TEMP_ROLE");
        role.CreatedAt = default;
        role.UpdatedAt = default;
        context.Roles.Add(role);

        await context.SaveChangesAsync();

        role.CreatedAt.Should().NotBe(default);
        role.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldUpdateUpdatedAt_OnModify()
    {
        await using var context = TestDbFactory.CreateContext();
        var role = TestDataFactory.CreateRole("EDITOR");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var before = role.UpdatedAt;

        await Task.Delay(5);
        role.Description = "Updated description";
        await context.SaveChangesAsync();

        role.UpdatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task GlobalSoftDeleteFilter_ShouldExcludeDeletedOperations()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var active = TestDataFactory.CreateOperation(userId, OperationType.Expense, 10m, "RUB", DateTime.UtcNow);
        var deleted = TestDataFactory.CreateOperation(userId, OperationType.Expense, 20m, "RUB", DateTime.UtcNow);
        deleted.DeletedAt = DateTime.UtcNow;
        context.FinancialOperations.AddRange(active, deleted);
        await context.SaveChangesAsync();

        var visible = await context.FinancialOperations.ToListAsync();
        var all = await context.FinancialOperations.IgnoreQueryFilters().ToListAsync();

        visible.Should().ContainSingle(x => x.Id == active.Id);
        visible.Should().NotContain(x => x.Id == deleted.Id);
        all.Should().HaveCount(2);
    }
}
