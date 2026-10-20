using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Controllers;
using Xunit;

namespace RaceDay.Tests;

public class ResultsControllerNoDbTests
{
    private static readonly Type ControllerType = typeof(ResultsController);

    private static IEnumerable<MethodInfo> Actions(string name) =>
        ControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                      .Where(m => m.Name == name);

    private static string[] RolesOn(MethodInfo method) =>
        (method.GetCustomAttribute<AuthorizeAttribute>()?.Roles ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Fact]
    public void Controller_RequiresLogin()
    {
        Assert.NotNull(ControllerType.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Capture_BothOverloads_AreForOrganisersAndAdmins()
    {
        var captures = Actions("Capture").ToList();

        Assert.Equal(2, captures.Count);   // GET and POST
        Assert.All(captures, m =>
        {
            Assert.Contains("Organiser", RolesOn(m));
            Assert.DoesNotContain("Participant", RolesOn(m));   // participants must not enter results
        });
    }

    [Fact]
    public void My_IsParticipantOnly()
    {
        var roles = RolesOn(Actions("My").Single());

        Assert.Equal(new[] { "Participant" }, roles);
    }

    [Fact]
    public void CapturePost_IsProtectedAgainstForgedRequests()
    {
        var post = Actions("Capture")
            .Single(m => m.GetCustomAttribute<HttpPostAttribute>() != null);

        Assert.NotNull(post.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void CaptureGet_IsNotAPostAction()
    {
        var get = Actions("Capture")
            .Single(m => m.GetCustomAttribute<HttpPostAttribute>() == null);

        Assert.Null(get.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void EveryPostAction_HasAntiForgeryProtection()
    {
        var posts = ControllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null)
            .ToList();

        Assert.NotEmpty(posts);
        Assert.All(posts, m => Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }
}