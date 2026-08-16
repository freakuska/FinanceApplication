using System.Security.Claims;
using FinanceApp.Api.Controllers;
using FinanceApp.Dbo.Enums;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceApp.Tests.Controllers;

public class TagsControllerTests
{
    private readonly Mock<ITagService> _service = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenTagMissing()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((TagDto)null!);

        var result = await controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPopular_ShouldReturnBadRequest_ForInvalidCount()
    {
        var controller = CreateController(_userId);

        var result = await controller.GetPopular(0);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_ShouldValidateQuery()
    {
        var controller = CreateController(_userId);

        (await controller.Search(string.Empty)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.Search("a")).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction_WhenAuthorized()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.CreateAsync(_userId, It.IsAny<CreateTagDto>())).ReturnsAsync(new TagDto
        {
            Id = Guid.NewGuid(),
            Name = "Food",
            Slug = "food",
            Type = "Expense",
            Icon = "i",
            Color = "#000",
            Visibility = "Private",
            Children = new List<TagDto>()
        });

        var result = await controller.Create(new CreateTagDto
        {
            Name = "Food",
            Type = TagType.Expense,
            Visibility = TagVisibility.Private
        });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenUnauthorized()
    {
        var controller = CreateController(null);

        var result = await controller.Create(new CreateTagDto
        {
            Name = "Food",
            Type = TagType.Expense,
            Visibility = TagVisibility.Private
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_ShouldMapExceptionsToHttpCodes()
    {
        var controller = CreateController(_userId);
        var id = Guid.NewGuid();
        _service.Setup(x => x.UpdateAsync(id, It.IsAny<UpdateTagDto>())).ThrowsAsync(new KeyNotFoundException("missing"));

        var missing = await controller.Update(id, new UpdateTagDto { Visibility = TagVisibility.Private });
        missing.Should().BeOfType<NotFoundObjectResult>();

        _service.Setup(x => x.UpdateAsync(id, It.IsAny<UpdateTagDto>())).ThrowsAsync(new InvalidOperationException("bad"));
        var invalid = await controller.Update(id, new UpdateTagDto { Visibility = TagVisibility.Private });
        invalid.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Delete_ShouldMapResultAndExceptions()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var notFound = await controller.Delete(Guid.NewGuid());
        notFound.Should().BeOfType<NotFoundObjectResult>();

        _service.Setup(x => x.DeleteAsync(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("linked"));
        var badRequest = await controller.Delete(Guid.NewGuid());
        badRequest.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangeVisibility_ShouldReturnNotFound_WhenServiceReturnsFalse()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.ChangeVisibilityAsync(It.IsAny<Guid>(), TagVisibility.Public, _userId)).ReturnsAsync(false);

        var result = await controller.ChangeVisibility(Guid.NewGuid(), new TagsController.ChangeVisibilityRequest
        {
            Visibility = TagVisibility.Public
        });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetAll_ShouldFilterByVisibility_WhenTypeSpecified()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetByTypeAsync(TagType.Expense, _userId)).ReturnsAsync(new List<TagDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "PublicTag",
                Slug = "public-tag",
                Type = "Expense",
                Icon = "i",
                Color = "#000",
                Visibility = "Public",
                Children = new List<TagDto>()
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "PrivateTag",
                Slug = "private-tag",
                Type = "Expense",
                Icon = "i",
                Color = "#000",
                Visibility = "Private",
                Children = new List<TagDto>()
            }
        });

        var result = await controller.GetAll(TagType.Expense, TagVisibility.Public);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var tags = ok.Value.Should().BeAssignableTo<List<TagDto>>().Subject;
        tags.Should().ContainSingle(x => x.Visibility == "Public");
    }

    [Fact]
    public async Task GetStats_ShouldReturnStubbedDictionary()
    {
        var controller = CreateController(_userId);

        var result = await controller.GetStats();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetByIdAndGetBySlug_ShouldReturnOk_WhenTagFound()
    {
        var controller = CreateController(_userId);
        var tag = new TagDto
        {
            Id = Guid.NewGuid(),
            Name = "Tag",
            Slug = "tag",
            Type = "Expense",
            Icon = "i",
            Color = "#000",
            Visibility = "Public",
            Children = new List<TagDto>()
        };
        _service.Setup(x => x.GetByIdAsync(tag.Id)).ReturnsAsync(tag);
        _service.Setup(x => x.GetBySlugAsync("tag")).ReturnsAsync(tag);

        (await controller.GetById(tag.Id)).Should().BeOfType<OkObjectResult>();
        (await controller.GetBySlug("tag")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBySlug_ShouldReturnNotFound_WhenTagMissing()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetBySlugAsync("missing")).ReturnsAsync((TagDto)null!);

        var result = await controller.GetBySlug("missing");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetByTypeAndGetTree_ShouldReturnOk()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetByTypeAsync(TagType.Expense, _userId)).ReturnsAsync(new List<TagDto>());
        _service.Setup(x => x.GetTreeAsync(TagType.Expense, _userId)).ReturnsAsync(new List<TagDto>());

        (await controller.GetByType(TagType.Expense)).Should().BeOfType<OkObjectResult>();
        (await controller.GetTree(TagType.Expense)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPopularAndSearch_ShouldReturnOk_WhenInputValid()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetPopularAsync(2)).ReturnsAsync(new List<TagDto>());
        _service.Setup(x => x.SearchAsync("food", _userId)).ReturnsAsync(new List<TagDto>());

        (await controller.GetPopular(2)).Should().BeOfType<OkObjectResult>();
        (await controller.Search("food")).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteAndChangeVisibility_ShouldReturnSuccess_WhenServiceAllows()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        _service.Setup(x => x.ChangeVisibilityAsync(It.IsAny<Guid>(), TagVisibility.Public, _userId)).ReturnsAsync(true);

        var deleteResult = await controller.Delete(Guid.NewGuid());
        var visibilityResult = await controller.ChangeVisibility(Guid.NewGuid(), new TagsController.ChangeVisibilityRequest
        {
            Visibility = TagVisibility.Public
        });

        deleteResult.Should().BeOfType<NoContentResult>();
        visibilityResult.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAll_ShouldReturnTree_WhenTypeNotSpecified()
    {
        var controller = CreateController(_userId);
        _service.Setup(x => x.GetTreeAsync(null, _userId)).ReturnsAsync(new List<TagDto>());

        var result = await controller.GetAll(null, null);

        result.Should().BeOfType<OkObjectResult>();
    }

    private TagsController CreateController(Guid? userId)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        return new TagsController(_service.Object)
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
