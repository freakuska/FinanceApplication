using System.Security.Claims;
using FinanceApp.Api.Controllers;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceApp.Tests.Controllers;

public class ReportsControllerTests
{
    private readonly Mock<IReportService> _service = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task GetMonthlyReport_ShouldParseTagIdsAndReturnOk()
    {
        var tag1 = Guid.NewGuid();
        var tag2 = Guid.NewGuid();
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetMonthlyReportAsync(
                _userId,
                2026,
                3,
                It.Is<List<Guid>?>(ids => ids != null && ids.SequenceEqual(new[] { tag1, tag2 }))))
            .ReturnsAsync(new MonthlyReportDto
            {
                Year = 2026,
                Month = 3,
                ByCurrency = new Dictionary<string, OperationStatsDto>(),
                ByCategory = new List<CategoryStatsDto>(),
                ByDay = new List<DailyStatsDto>()
            });

        var result = await controller.GetMonthlyReport(2026, 3, $"{tag1},{tag2}");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetYearlyReport_ShouldReturnOk()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetYearlyReportAsync(_userId, 2026, null)).ReturnsAsync(new YearlyReportDto
        {
            Year = 2026,
            ByCurrency = new Dictionary<string, OperationStatsDto>(),
            ByCategory = new List<CategoryStatsDto>(),
            ByMonth = new List<MonthlyStatsDto>()
        });

        var result = await controller.GetYearlyReport(2026);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCategoryReport_ShouldReturnOk()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetCategoryReportAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(new CategoryReportDto { Categories = new List<CategoryStatsDto>() });

        var result = await controller.GetCategoryReport(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetTrendReport_ShouldPassGroupByAndReturnOk()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetTrendReportAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), "week", null))
            .ReturnsAsync(new TrendReportDto { Data = new List<TrendDataDto>(), GroupBy = "week" });

        var result = await controller.GetTrendReport(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow, "week");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExportToCsv_ShouldReturnFileResult()
    {
        var controller = CreateController(_userId);
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 3, 31);
        _service.Setup(x => x.ExportToCsvAsync(_userId, start, end)).ReturnsAsync(new byte[] { 1, 2, 3 });

        var result = await controller.ExportToCsv(start, end);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileDownloadName.Should().Be("report_2026-03-01_2026-03-31.csv");
    }

    [Fact]
    public async Task ExportToExcel_ShouldReturnFileResult()
    {
        var controller = CreateController(_userId);
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 3, 31);
        _service.Setup(x => x.ExportToExcelAsync(_userId, start, end)).ReturnsAsync(new byte[] { 1, 2, 3 });

        var result = await controller.ExportToExcel(start, end);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        file.FileDownloadName.Should().Be("report_2026-03-01_2026-03-31.xlsx");
    }

    [Fact]
    public async Task Actions_ShouldThrowUnauthorized_WhenUserMissing()
    {
        var controller = CreateController(null);

        var action = () => controller.GetYearlyReport(2026);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private ReportsController CreateController(Guid? userId)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        return new ReportsController(_service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId.HasValue ? "Test" : null))
                }
            }
        };
    }
}
