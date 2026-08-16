using System.Security.Claims;
using FinanceApp.Api.Controllers;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceApp.Tests.Controllers;

public class OperationsControllerTests
{
    private readonly Mock<IFinancialOperationService> _service = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task GetOperations_ShouldReturnOkWithPagedResult()
    {
        var controller = CreateController(_userId);
        var filter = new OperationFilterDto { Page = 1, PageSize = 10 };
        _service.Setup(x => x.GetPagedAsync(_userId, filter)).ReturnsAsync(new PagedResult<OperationDto>
        {
            Items = new List<OperationDto> { BuildOperationDto() },
            TotalCount = 1,
            Page = 1,
            PageSize = 10
        });

        var result = await controller.GetOperations(filter);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<PagedResult<OperationDto>>();
    }

    [Fact]
    public async Task GetOperation_ShouldReturnNotFound_WhenMissing()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), _userId)).ReturnsAsync((OperationDto)null!);

        var result = await controller.GetOperation(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateOperation_ShouldReturnCreatedAtAction()
    {
        var controller = CreateController(_userId);
        var created = BuildOperationDto();
        _service.Setup(x => x.CreateAsync(It.IsAny<CreateOperationDto>(), _userId)).ReturnsAsync(created);

        var result = await controller.CreateOperation(new CreateOperationDto
        {
            Type = FinanceApp.Dbo.Enums.OperationType.Expense,
            Amount = 100,
            Currency = "RUB",
            PaymentMethod = FinanceApp.Dbo.Enums.PaymentMethod.Card
        });

        var createdAt = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdAt.ActionName.Should().Be(nameof(OperationsController.GetOperation));
    }

    [Fact]
    public async Task UpdateOperation_ShouldReturnNotFound_WhenServiceThrowsKeyNotFound()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateOperationDto>(), _userId))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await controller.UpdateOperation(Guid.NewGuid(), new UpdateOperationDto());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteOperation_ShouldReturnNoContent_WhenDeleted()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), _userId)).ReturnsAsync(true);

        var result = await controller.DeleteOperation(Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteOperation_ShouldReturnNotFound_WhenDeleteFailed()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), _userId)).ReturnsAsync(false);

        var result = await controller.DeleteOperation(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RestoreOperation_ShouldReturnOk_WhenRestored()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.RestoreAsync(It.IsAny<Guid>(), _userId)).ReturnsAsync(true);

        var result = await controller.RestoreOperation(Guid.NewGuid());

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task GetStats_ShouldReturnOk()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetStatsByCurrencyAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<string, OperationStatsDto>
            {
                ["RUB"] = new OperationStatsDto
                {
                    Currency = "RUB",
                    TotalIncome = 100,
                    TotalExpense = 50,
                    Balance = 50,
                    Count = 2
                }
            });

        var result = await controller.GetStats(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Actions_ShouldThrowUnauthorized_WhenUserClaimMissing()
    {
        var controller = CreateController(null);

        var action = () => controller.GetOperations(new OperationFilterDto());

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private OperationsController CreateController(Guid? userId)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, userId.HasValue ? "Test" : null);
        var principal = new ClaimsPrincipal(identity);

        return new OperationsController(_service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static OperationDto BuildOperationDto()
    {
        return new OperationDto
        {
            Id = Guid.NewGuid(),
            Type = "Expense",
            Money = new MoneyDto { Amount = 10, Currency = "RUB" },
            PaymentMethod = "Card",
            Description = "test",
            Notes = string.Empty,
            OperationDateTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Tags = new List<TagDto>()
        };
    }
}
