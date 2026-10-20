using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaceDay.Controllers;
using RaceDay.Data;
using RaceDay.Models.ViewModels;
using Xunit;

namespace RaceDay.Tests;

public class UserRolesNoDbTests
{
    // ---------- Helpers ----------

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<RoleManager<IdentityRole>> MockRoleManager(params IdentityRole[] roles)
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        var mgr = new Mock<RoleManager<IdentityRole>>(store.Object, null!, null!, null!, null!);
        mgr.Setup(r => r.Roles).Returns(roles.AsQueryable());
        return mgr;
    }

    private static UserRoles CreateController(
        Mock<UserManager<ApplicationUser>> users,
        Mock<RoleManager<IdentityRole>>? roles = null)
        => new(users.Object, (roles ?? MockRoleManager()).Object);

    private static MethodInfo ManageMethod(bool post) =>
        typeof(UserRoles).GetMethods()
            .Where(m => m.Name == "Manage")
            .Single(m => (m.GetCustomAttribute<HttpPostAttribute>() != null) == post);

    // ---------- Authorization rules ----------

    [Fact]
    public void Controller_RequiresAnAuthorizedRole()
    {
        var attr = typeof(UserRoles).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.False(string.IsNullOrWhiteSpace(attr!.Roles));
    }

    [Fact]
    public void Controller_AllowsAdmin()
    {
        var roles = typeof(UserRoles).GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');

        Assert.Contains("Admin", roles);
    }

    [Fact]
    public void ManagePost_IsProtectedAgainstForgedRequests()
    {
        Assert.NotNull(ManageMethod(post: true).GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    // This test FAILS until you change [Authorize(Roles = "Admin,Organiser")] to "Admin".
    // Today any Organiser can give themselves the Admin role.
    [Fact]
    public void Controller_IsNotOpenToOrganisers()
    {
        var roles = typeof(UserRoles).GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');

        Assert.DoesNotContain("Organiser", roles);
    }

    // ---------- Manage (GET) ----------

    [Fact]
    public async Task Manage_Get_UnknownUser_SetsErrorMessageContainingTheId()
    {
        var controller = CreateController(MockUserManager());   // FindByIdAsync returns null

        await controller.Manage("missing-id");

        Assert.Contains("missing-id", (string)controller.ViewBag.ErrorMessage);
    }

    // This test FAILS until the typo View("Not Found") is changed to View("NotFound").
    [Fact]
    public async Task Manage_Get_UnknownUser_ReturnsTheNotFoundView()
    {
        var controller = CreateController(MockUserManager());

        var result = await controller.Manage("missing-id");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("NotFound", view.ViewName);
    }

    [Fact]
    public async Task Manage_Get_MarksOnlyTheUsersRolesAsSelected()
    {
        var user = new ApplicationUser { Id = "1", UserName = "alice" };
        var users = MockUserManager();
        users.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        users.Setup(u => u.IsInRoleAsync(user, "Organiser")).ReturnsAsync(true);
        users.Setup(u => u.IsInRoleAsync(user, "Participant")).ReturnsAsync(false);
        var roles = MockRoleManager(
            new IdentityRole { Id = "r1", Name = "Organiser" },
            new IdentityRole { Id = "r2", Name = "Participant" });
        var controller = CreateController(users, roles);

        var result = await controller.Manage("1");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<ManageUserRolesViewModel>>(view.Model);
        Assert.True(model.Single(m => m.RoleName == "Organiser").isSelected);
        Assert.False(model.Single(m => m.RoleName == "Participant").isSelected);
    }

    // ---------- Manage (POST) ----------

    [Fact]
    public async Task Manage_Post_UnknownUser_ReturnsNotFoundView()
    {
        var controller = CreateController(MockUserManager());

        var result = await controller.Manage(new List<ManageUserRolesViewModel>(), "missing-id");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("NotFound", view.ViewName);
    }

    [Fact]
    public async Task Manage_Post_RemoveFails_ShowsErrorAndDoesNotAddAnyRoles()
    {
        var user = new ApplicationUser { Id = "1" };
        var users = MockUserManager();
        users.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        users.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Participant" });
        users.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
             .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "fail" }));
        var controller = CreateController(users);

        var result = await controller.Manage(new List<ManageUserRolesViewModel>(), "1");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        users.Verify(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()),
                     Times.Never);
    }

    [Fact]
    public async Task Manage_Post_Success_AddsOnlyTheTickedRolesAndRedirectsToIndex()
    {
        var user = new ApplicationUser { Id = "1" };
        var users = MockUserManager();
        users.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        users.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Participant" });
        users.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
             .ReturnsAsync(IdentityResult.Success);
        users.Setup(u => u.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>()))
             .ReturnsAsync(IdentityResult.Success);
        var controller = CreateController(users);
        var model = new List<ManageUserRolesViewModel>
        {
            new() { RoleName = "Organiser", isSelected = true },
            new() { RoleName = "Participant", isSelected = false }
        };

        var result = await controller.Manage(model, "1");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        users.Verify(u => u.AddToRolesAsync(user,
            It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Organiser" }))), Times.Once);
    }

    [Fact]
    public async Task Manage_Post_AddFails_ShowsError()
    {
        var user = new ApplicationUser { Id = "1" };
        var users = MockUserManager();
        users.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        users.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        users.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
             .ReturnsAsync(IdentityResult.Success);
        users.Setup(u => u.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>()))
             .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "fail" }));
        var controller = CreateController(users);

        var result = await controller.Manage(
            new List<ManageUserRolesViewModel> { new() { RoleName = "Organiser", isSelected = true } }, "1");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }
}