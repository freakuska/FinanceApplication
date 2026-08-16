using FinanceApp.Dbo.Enums;
using FinanceApp.Dbo.Models;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Tests.Services;

public class FinancialOperationServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldCreateOperationWithTagsAndIncreaseUsage()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var tag = TestDataFactory.CreateTag("Food", TagType.Expense, ownerId: userId, usageCount: 0);
        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var created = await service.CreateAsync(new CreateOperationDto
        {
            Type = OperationType.Expense,
            Amount = 123.45m,
            Currency = "usd",
            PaymentMethod = PaymentMethod.Card,
            Description = "Lunch",
            Notes = "Business lunch",
            OperationDateTime = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
            TagIds = new List<Guid> { tag.Id }
        }, userId);

        created.Should().NotBeNull();
        created.Type.Should().Be("Expense");
        created.Money.Currency.Should().Be("USD");
        created.Tags.Should().ContainSingle(t => t.Id == tag.Id);
        (await context.Tags.FindAsync(tag.Id))!.UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenOperationMissing()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new FinancialOperationService(context);

        var action = () => service.UpdateAsync(Guid.NewGuid(), new UpdateOperationDto(), Guid.NewGuid());

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Operation not found*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldReplaceTagsAndUpdateFields()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var tag1 = TestDataFactory.CreateTag("Tag1", TagType.Expense, ownerId: userId);
        var tag2 = TestDataFactory.CreateTag("Tag2", TagType.Expense, ownerId: userId);
        var operation = TestDataFactory.CreateOperation(userId, OperationType.Expense, 100, "RUB", DateTime.UtcNow);
        context.AddRange(tag1, tag2, operation);
        await context.SaveChangesAsync();
        context.OperationTags.Add(new OperationTag { Id = Guid.NewGuid(), OperationId = operation.Id, TagId = tag1.Id });
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var updated = await service.UpdateAsync(operation.Id, new UpdateOperationDto
        {
            Amount = 500m,
            Currency = "eur",
            PaymentMethod = PaymentMethod.Transfer,
            Description = "Updated",
            Notes = "Updated notes",
            TagIds = new List<Guid> { tag2.Id }
        }, userId);

        updated.Money.Amount.Should().Be(500m);
        updated.Money.Currency.Should().Be("EUR");
        updated.PaymentMethod.Should().Be("Transfer");
        updated.Tags.Should().ContainSingle(t => t.Id == tag2.Id);

        var links = await context.OperationTags.Where(x => x.OperationId == operation.Id).ToListAsync();
        links.Should().ContainSingle(x => x.TagId == tag2.Id);
    }

    [Fact]
    public async Task DeleteAndRestore_ShouldToggleDeletedAt()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var operation = TestDataFactory.CreateOperation(userId, OperationType.Expense, 100m, "RUB", DateTime.UtcNow);
        context.FinancialOperations.Add(operation);
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var deleted = await service.DeleteAsync(operation.Id, userId);
        var hiddenByFilter = await context.FinancialOperations.FirstOrDefaultAsync(x => x.Id == operation.Id);
        var fromIgnoreFilter = await context.FinancialOperations.IgnoreQueryFilters().SingleAsync(x => x.Id == operation.Id);

        deleted.Should().BeTrue();
        hiddenByFilter.Should().BeNull();
        fromIgnoreFilter.DeletedAt.Should().NotBeNull();

        var restored = await service.RestoreAsync(operation.Id, userId);
        restored.Should().BeTrue();
        (await context.FinancialOperations.SingleAsync(x => x.Id == operation.Id)).DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_ShouldApplyTypeCurrencyAndTagFilters()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var expenseTag = TestDataFactory.CreateTag("ExpenseTag", TagType.Expense, ownerId: userId);
        var incomeTag = TestDataFactory.CreateTag("IncomeTag", TagType.Income, ownerId: userId);
        var expense = TestDataFactory.CreateOperation(userId, OperationType.Expense, 10m, "USD", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var income = TestDataFactory.CreateOperation(userId, OperationType.Income, 20m, "USD", new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(expenseTag, incomeTag, expense, income);
        await context.SaveChangesAsync();
        context.OperationTags.AddRange(
            new OperationTag { Id = Guid.NewGuid(), OperationId = expense.Id, TagId = expenseTag.Id },
            new OperationTag { Id = Guid.NewGuid(), OperationId = income.Id, TagId = incomeTag.Id });
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var result = await service.GetPagedAsync(userId, new OperationFilterDto
        {
            Type = OperationType.Expense,
            Currency = "usd",
            TagIds = new List<Guid> { expenseTag.Id },
            Page = 1,
            PageSize = 10
        });

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].Type.Should().Be("Expense");
    }

    [Fact]
    public async Task GetStatsByCurrencyAsync_ShouldCalculateTotals()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        context.FinancialOperations.AddRange(
            TestDataFactory.CreateOperation(userId, OperationType.Income, 100m, "RUB", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            TestDataFactory.CreateOperation(userId, OperationType.Expense, 40m, "RUB", new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)),
            TestDataFactory.CreateOperation(userId, OperationType.Expense, 10m, "USD", new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var stats = await service.GetStatsByCurrencyAsync(
            userId,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc));

        stats.Should().ContainKey("RUB");
        stats["RUB"].TotalIncome.Should().Be(100m);
        stats["RUB"].TotalExpense.Should().Be(40m);
        stats["RUB"].Balance.Should().Be(60m);
        stats["USD"].TotalExpense.Should().Be(10m);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenOperationBelongsToAnotherUser()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var operation = TestDataFactory.CreateOperation(ownerId, OperationType.Income, 100m, "RUB", DateTime.UtcNow);
        context.FinancialOperations.Add(operation);
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var result = await service.GetByIdAsync(operation.Id, anotherUserId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTypeAndOperationDate_WhenProvided()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var operation = TestDataFactory.CreateOperation(
            userId,
            OperationType.Expense,
            5m,
            "RUB",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        context.FinancialOperations.Add(operation);
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var updated = await service.UpdateAsync(operation.Id, new UpdateOperationDto
        {
            Type = OperationType.Income,
            OperationDateTime = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        }, userId);

        updated.Type.Should().Be("Income");
        updated.OperationDateTime.Should().Be(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task DeleteAndRestore_ShouldReturnFalse_WhenStateDoesNotAllowOperation()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var operation = TestDataFactory.CreateOperation(userId, OperationType.Expense, 1m, "RUB", DateTime.UtcNow);
        context.FinancialOperations.Add(operation);
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var restoreBeforeDelete = await service.RestoreAsync(operation.Id, userId);
        var firstDelete = await service.DeleteAsync(operation.Id, userId);
        var secondDelete = await service.DeleteAsync(operation.Id, userId);

        restoreBeforeDelete.Should().BeFalse();
        firstDelete.Should().BeTrue();
        secondDelete.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserOperationsAsync_ShouldApplyAllFilters()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var tag1 = TestDataFactory.CreateTag("T1", TagType.Expense, ownerId: userId);
        var tag2 = TestDataFactory.CreateTag("T2", TagType.Expense, ownerId: userId);
        var op1 = TestDataFactory.CreateOperation(userId, OperationType.Expense, 10m, "USD", new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));
        var op2 = TestDataFactory.CreateOperation(userId, OperationType.Income, 20m, "USD", new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc));
        var op3 = TestDataFactory.CreateOperation(userId, OperationType.Expense, 30m, "EUR", new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(tag1, tag2, op1, op2, op3);
        await context.SaveChangesAsync();
        context.OperationTags.AddRange(
            new OperationTag { Id = Guid.NewGuid(), OperationId = op1.Id, TagId = tag1.Id },
            new OperationTag { Id = Guid.NewGuid(), OperationId = op2.Id, TagId = tag1.Id },
            new OperationTag { Id = Guid.NewGuid(), OperationId = op3.Id, TagId = tag2.Id });
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var result = await service.GetUserOperationsAsync(
            userId,
            startDate: new DateTime(2026, 4, 9, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 4, 11, 23, 59, 59, DateTimeKind.Utc),
            type: OperationType.Expense,
            currency: "usd",
            tagIds: new List<Guid> { tag1.Id },
            page: 1,
            pageSize: 10);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(op1.Id);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldApplyDateRangeFilters()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        context.FinancialOperations.AddRange(
            TestDataFactory.CreateOperation(userId, OperationType.Income, 1m, "USD", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)),
            TestDataFactory.CreateOperation(userId, OperationType.Income, 2m, "USD", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var service = new FinancialOperationService(context);

        var paged = await service.GetPagedAsync(userId, new OperationFilterDto
        {
            StartDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            Page = 1,
            PageSize = 10
        });

        paged.TotalCount.Should().Be(1);
    }
}
