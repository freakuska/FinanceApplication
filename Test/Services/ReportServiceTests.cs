using System.Text;
using FinanceApp.Dbo.Enums;
using FinanceApp.Dbo.Models;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Tests.TestInfrastructure;
using FluentAssertions;

namespace FinanceApp.Tests.Services;

public class ReportServiceTests
{
    [Fact]
    public async Task GetMonthlyReportAsync_ShouldReturnCurrencyCategoryAndDayAggregations()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var foodTag = TestDataFactory.CreateTag("Food", TagType.Expense, ownerId: userId);
        var salaryTag = TestDataFactory.CreateTag("Salary", TagType.Income, ownerId: userId, visibility: TagVisibility.Public);

        var income = TestDataFactory.CreateOperation(userId, OperationType.Income, 3000m, "USD", new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc));
        var expense = TestDataFactory.CreateOperation(userId, OperationType.Expense, 500m, "USD", new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(foodTag, salaryTag, income, expense);
        await context.SaveChangesAsync();
        context.OperationTags.AddRange(
            new OperationTag { Id = Guid.NewGuid(), OperationId = income.Id, TagId = salaryTag.Id },
            new OperationTag { Id = Guid.NewGuid(), OperationId = expense.Id, TagId = foodTag.Id });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var report = await service.GetMonthlyReportAsync(userId, 2026, 2);

        report.ByCurrency.Should().ContainKey("USD");
        report.ByCurrency["USD"].TotalIncome.Should().Be(3000m);
        report.ByCurrency["USD"].TotalExpense.Should().Be(500m);
        report.ByCategory.Should().ContainSingle(c => c.TagName == "Food");
        report.ByDay.Should().ContainSingle();
    }

    [Fact]
    public async Task GetYearlyReportAsync_ShouldGroupByMonth()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        context.FinancialOperations.AddRange(
            TestDataFactory.CreateOperation(userId, OperationType.Income, 100m, "RUB", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            TestDataFactory.CreateOperation(userId, OperationType.Income, 200m, "RUB", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var report = await service.GetYearlyReportAsync(userId, 2026);

        report.ByMonth.Should().HaveCount(2);
        report.ByMonth.Select(x => x.Month).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task GetCategoryReportAsync_ShouldCalculatePercentagesPerCurrency()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var rent = TestDataFactory.CreateTag("Rent", TagType.Expense, ownerId: userId);
        var food = TestDataFactory.CreateTag("Food", TagType.Expense, ownerId: userId);
        var op1 = TestDataFactory.CreateOperation(userId, OperationType.Expense, 800m, "USD", new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        var op2 = TestDataFactory.CreateOperation(userId, OperationType.Expense, 200m, "USD", new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(rent, food, op1, op2);
        await context.SaveChangesAsync();
        context.OperationTags.AddRange(
            new OperationTag { Id = Guid.NewGuid(), OperationId = op1.Id, TagId = rent.Id },
            new OperationTag { Id = Guid.NewGuid(), OperationId = op2.Id, TagId = food.Id });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var report = await service.GetCategoryReportAsync(
            userId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc));

        report.Categories.Should().HaveCount(2);
        report.Categories.Single(c => c.TagName == "Rent").Percentage.Should().Be(80m);
        report.Categories.Single(c => c.TagName == "Food").Percentage.Should().Be(20m);
    }

    [Fact]
    public async Task GetTrendReportAsync_ShouldRespectMonthGrouping()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        context.FinancialOperations.AddRange(
            TestDataFactory.CreateOperation(userId, OperationType.Income, 100m, "RUB", new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            TestDataFactory.CreateOperation(userId, OperationType.Expense, 30m, "RUB", new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
            TestDataFactory.CreateOperation(userId, OperationType.Income, 50m, "RUB", new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var report = await service.GetTrendReportAsync(
            userId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            groupBy: "month");

        report.GroupBy.Should().Be("month");
        report.Data.Should().HaveCount(2);
        report.Data[0].Income.Should().Be(100m);
        report.Data[0].Expense.Should().Be(30m);
    }

    [Fact]
    public async Task ExportToCsvAsync_ShouldReturnCsvPayload()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var tag = TestDataFactory.CreateTag("Food", TagType.Expense, ownerId: userId);
        var op = TestDataFactory.CreateOperation(userId, OperationType.Expense, 50m, "USD", new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc), "Breakfast");
        context.AddRange(tag, op);
        await context.SaveChangesAsync();
        context.OperationTags.Add(new OperationTag { Id = Guid.NewGuid(), OperationId = op.Id, TagId = tag.Id });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var bytes = await service.ExportToCsvAsync(
            userId,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc));
        var csv = Encoding.UTF8.GetString(bytes);

        csv.Should().Contain("Date,Type,Amount,Currency,Payment Method,Description,Tags");
        csv.Should().Contain("Breakfast");
        csv.Should().Contain("Food");
    }

    [Fact]
    public async Task ExportToExcelAsync_ShouldFallbackToCsvBytes()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        context.FinancialOperations.Add(
            TestDataFactory.CreateOperation(userId, OperationType.Income, 1m, "USD", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), "Income"));
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var csvBytes = await service.ExportToCsvAsync(
            userId,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc));
        var excelBytes = await service.ExportToExcelAsync(
            userId,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc));

        excelBytes.Should().Equal(csvBytes);
    }

    [Fact]
    public async Task GetMonthlyReportAsync_ShouldApplyTagFilter()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var tagA = TestDataFactory.CreateTag("TagA", TagType.Expense, ownerId: userId);
        var tagB = TestDataFactory.CreateTag("TagB", TagType.Expense, ownerId: userId);
        var opA = TestDataFactory.CreateOperation(userId, OperationType.Expense, 100m, "USD", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var opB = TestDataFactory.CreateOperation(userId, OperationType.Expense, 300m, "USD", new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(tagA, tagB, opA, opB);
        await context.SaveChangesAsync();
        context.OperationTags.AddRange(
            new OperationTag { Id = Guid.NewGuid(), OperationId = opA.Id, TagId = tagA.Id },
            new OperationTag { Id = Guid.NewGuid(), OperationId = opB.Id, TagId = tagB.Id });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var report = await service.GetMonthlyReportAsync(userId, 2026, 7, new List<Guid> { tagA.Id });

        report.ByCurrency["USD"].TotalExpense.Should().Be(100m);
        report.ByCategory.Should().ContainSingle(c => c.TagName == "TagA");
    }

    [Fact]
    public async Task GetYearlyReportAsync_ShouldApplyTagFilter()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var tagA = TestDataFactory.CreateTag("YTagA", TagType.Expense, ownerId: userId);
        var tagB = TestDataFactory.CreateTag("YTagB", TagType.Expense, ownerId: userId);
        var opA = TestDataFactory.CreateOperation(userId, OperationType.Expense, 100m, "RUB", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var opB = TestDataFactory.CreateOperation(userId, OperationType.Expense, 500m, "RUB", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(tagA, tagB, opA, opB);
        await context.SaveChangesAsync();
        context.OperationTags.AddRange(
            new OperationTag { Id = Guid.NewGuid(), OperationId = opA.Id, TagId = tagA.Id },
            new OperationTag { Id = Guid.NewGuid(), OperationId = opB.Id, TagId = tagB.Id });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var report = await service.GetYearlyReportAsync(userId, 2026, new List<Guid> { tagA.Id });

        report.ByCurrency["RUB"].TotalExpense.Should().Be(100m);
        report.ByCategory.Should().ContainSingle(c => c.TagName == "YTagA");
    }

    [Fact]
    public async Task GetCategoryReportAsync_ShouldHandleNonUtcDates()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var tag = TestDataFactory.CreateTag("LocalDateTag", TagType.Expense, ownerId: userId);
        var op = TestDataFactory.CreateOperation(userId, OperationType.Expense, 50m, "USD", new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(tag, op);
        await context.SaveChangesAsync();
        context.OperationTags.Add(new OperationTag { Id = Guid.NewGuid(), OperationId = op.Id, TagId = tag.Id });
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var report = await service.GetCategoryReportAsync(
            userId,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Local),
            new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Local));

        report.Categories.Should().ContainSingle(c => c.TagName == "LocalDateTag");
    }

    [Fact]
    public async Task GetTrendReportAsync_ShouldSupportWeekAndDayGrouping()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        context.FinancialOperations.AddRange(
            TestDataFactory.CreateOperation(userId, OperationType.Income, 100m, "RUB", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
            TestDataFactory.CreateOperation(userId, OperationType.Expense, 30m, "RUB", new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)),
            TestDataFactory.CreateOperation(userId, OperationType.Income, 50m, "RUB", new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var weekly = await service.GetTrendReportAsync(
            userId,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Local),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Local),
            groupBy: "week");
        var daily = await service.GetTrendReportAsync(
            userId,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            groupBy: "day");

        weekly.Data.Count.Should().BeGreaterThanOrEqualTo(2);
        daily.Data.Count.Should().Be(3);
    }

    [Fact]
    public async Task ExportToCsvAsync_ShouldHandleNonUtcDateArguments()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        context.FinancialOperations.Add(
            TestDataFactory.CreateOperation(userId, OperationType.Income, 10m, "USD", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), "NonUtc"));
        await context.SaveChangesAsync();
        var service = new ReportService(context);

        var bytes = await service.ExportToCsvAsync(
            userId,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Local),
            new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Local));

        Encoding.UTF8.GetString(bytes).Should().Contain("NonUtc");
    }
}
