using Microsoft.AspNetCore.Mvc;
using RaceDay.Controllers;
using Xunit;

namespace RaceDay.Tests;

public class CategoriesControllerTests
{
    // Safe only for methods that return before using _db or _users
    private static CategoriesController CreateController() => new(null!, null!);

    [Fact]
    public async Task Edit_Get_NullId_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Edit((int?)null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_Get_NullId_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.Delete(null);

        Assert.IsType<NotFoundResult>(result);
    }
}