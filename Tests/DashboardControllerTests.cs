using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaceDay.Controllers;
using RaceDay.Data;
using Xunit;

namespace RaceDay.Tests;

public class DashboardControllerNoDbTests
{
    // ---------- Helpers ----------

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    // The database is never reached by the code paths tested here, so null is safe
    private static DashboardController CreateController(
        Mock<UserManager<ApplicationUser>> users, params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        var controller = new DashboardController(null!, users.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
        return controller;
    }

    private static string[] AllowedRoles() =>
        typeof(DashboardController).GetCustomAttribute<AuthorizeAttribute>()!
            .Roles!.Split(',', StringSplitOptions.TrimEntries);

    // ---------- Index: early exits ----------

    [Fact]
    public async Task Index_UserNotFound_ReturnsChallenge()
    {
        var users = MockUserManager();
        users.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
             .ReturnsAsync((ApplicationUser?)null);
        var controller = CreateController(users, "Organiser");

        var result = await controller.Index();

        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Index_UserHasNoRecognisedRole_ReturnsForbid()
    {
        var users = MockUserManager();
        users.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
             .ReturnsAsync(new ApplicationUser { Id = "user-1" });
        var controller = CreateController(users);   // signed in, but no roles

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_UserWithUnrelatedRole_ReturnsForbid()
    {
        var users = MockUserManager();
        users.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
             .ReturnsAsync(new ApplicationUser { Id = "user-1" });
        var controller = CreateController(users, "Visitor");

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    // ---------- Authorization rules ----------

    [Fact]
    public void Controller_RequiresLogin()
    {
        Assert.NotNull(typeof(DashboardController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Controller_AllowsOrganisersAndAdmins()
    {
        Assert.Contains("Admin", AllowedRoles());
        Assert.Contains("Organiser", AllowedRoles());
    }

    [Fact]
    public void Index_IsNotOpenToAnonymousUsers()
    {
        var index = typeof(DashboardController).GetMethod("Index")!;

        Assert.Null(index.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    // This test FAILS until you add "Participant" to the [Authorize] roles on the controller.
    // Today the Participant dashboard code in Index can never run, because
    // participants are rejected before the action is reached.
    [Fact]
    public void Controller_AllowsParticipants_SoTheirDashboardIsReachable()
    {
        Assert.Contains("Participant", AllowedRoles());
    }
}