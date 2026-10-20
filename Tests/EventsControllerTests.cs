using Microsoft.AspNetCore.Mvc;
using RaceDay.Controllers;
using RaceDay.Models;

using Xunit;

namespace RaceDay.Tests;

public class EventsControllerTests
{
    // Safe only for methods that return before using _db or _users
    private static EventsController CreateController() => new(null!, null!);

    // ---------- Details ----------

    [Fact]
    public async Task Details_NullId_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Details(null);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- Create (GET) ----------

    [Fact]
    public void Create_Get_ReturnsViewResult()
    {
        var controller = CreateController();

        var result = controller.Create();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Create_Get_ModelIsAnEvent()
    {
        var controller = CreateController();

        var result = controller.Create();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<Event>(view.Model);
    }

    [Fact]
    public void Create_Get_DefaultDateIs30DaysAheadAt8AM()
    {
        var controller = CreateController();

        var view = Assert.IsType<ViewResult>(controller.Create());
        var model = Assert.IsType<Event>(view.Model);

        Assert.Equal(30, (model.Date.Date - DateTime.Today).Days);
        Assert.Equal(8, model.Date.Hour);
    }

    // ---------- Create (POST) ----------

    [Fact]
    public async Task Create_Post_InvalidModel_ReturnsSameViewModel()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("Name", "Name is required");
        var ev = new Event();

        var result = await controller.Create(ev);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(ev, view.Model);   // the form is redisplayed with what the user typed
    }

    // ---------- Edit (GET) ----------

    [Fact]
    public async Task Edit_Get_NullId_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Edit((int?)null);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- Delete (GET) ----------

    [Fact]
    public async Task Delete_Get_NullId_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Delete(null);

        Assert.IsType<NotFoundResult>(result);
    }
}