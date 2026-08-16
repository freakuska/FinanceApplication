using FinanceApp.Web.Controllers;
using FinanceApp.Web.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceApp.Tests.Web;

public class WebControllersTests
{
    [Fact]
    public void HomeController_Actions_ShouldReturnViews()
    {
        var controller = new HomeController(Mock.Of<ILogger<HomeController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.Index().Should().BeOfType<ViewResult>();
        controller.Privacy().Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void HomeController_Error_ShouldReturnErrorViewModel()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-id";
        var controller = new HomeController(Mock.Of<ILogger<HomeController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = controller.Error();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeAssignableTo<ErrorViewModel>();
        ((ErrorViewModel)view.Model!).RequestId.Should().Be("trace-id");
    }

    [Fact]
    public void AccountController_Actions_ShouldReturnViews()
    {
        var controller = new AccountController();

        controller.Login().Should().BeOfType<ViewResult>();
        controller.Register().Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void OtherWebControllers_ShouldReturnViews()
    {
        new DashboardController().Index().Should().BeOfType<ViewResult>();
        new FinanceApp.Web.Controllers.ReportsController().Index().Should().BeOfType<ViewResult>();
        new FinanceApp.Web.Controllers.TagsController().Index().Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void ErrorViewModel_ShowRequestId_ShouldReflectRequestIdState()
    {
        var withId = new ErrorViewModel { RequestId = "req-1" };
        var withoutId = new ErrorViewModel { RequestId = null };

        withId.ShowRequestId.Should().BeTrue();
        withoutId.ShowRequestId.Should().BeFalse();
    }
}
