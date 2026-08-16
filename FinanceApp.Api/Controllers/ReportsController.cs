using FinanceApp.Api.Extensions;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("monthly/{year}/{month}")]
    public async Task<IActionResult> GetMonthlyReport(int year, int month, [FromQuery] string? tagIds = null)
    {
        var userId = GetCurrentUserId();
        var tags = ParseTagIds(tagIds);
        var report = await _reportService.GetMonthlyReportAsync(userId, year, month, tags);
        return Ok(report);
    }

    [HttpGet("yearly/{year}")]
    public async Task<IActionResult> GetYearlyReport(int year, [FromQuery] string? tagIds = null)
    {
        var userId = GetCurrentUserId();
        var tags = ParseTagIds(tagIds);
        var report = await _reportService.GetYearlyReportAsync(userId, year, tags);
        return Ok(report);
    }

    [HttpGet("category")]
    public async Task<IActionResult> GetCategoryReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string? tagIds = null)
    {
        var userId = GetCurrentUserId();
        var tags = ParseTagIds(tagIds);
        var report = await _reportService.GetCategoryReportAsync(userId, startDate, endDate, tags);
        return Ok(report);
    }

    [HttpGet("trend")]
    public async Task<IActionResult> GetTrendReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string groupBy = "day",
        [FromQuery] string? tagIds = null)
    {
        var userId = GetCurrentUserId();
        var tags = ParseTagIds(tagIds);
        var report = await _reportService.GetTrendReportAsync(userId, startDate, endDate, groupBy, tags);
        return Ok(report);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportToCsv(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        var userId = GetCurrentUserId();
        var csvData = await _reportService.ExportToCsvAsync(userId, startDate, endDate);
        return File(csvData, "text/csv", $"report_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}.csv");
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportToExcel(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        var userId = GetCurrentUserId();
        var excelData = await _reportService.ExportToExcelAsync(userId, startDate, endDate);
        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"report_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}.xlsx");
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
            throw new UnauthorizedAccessException("Пользователь не авторизован");
        return userId.Value;
    }

    private static List<Guid>? ParseTagIds(string? tagIds)
    {
        if (string.IsNullOrWhiteSpace(tagIds)) return null;
        return tagIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
