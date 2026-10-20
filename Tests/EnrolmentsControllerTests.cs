using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RaceDay.Controllers;
using RaceDay.Models;
using RaceDay.Models.ViewModels;
using Xunit;

namespace RaceDay.Tests;

public class EnrolmentsControllerNoDbTests
{
    // ---------- Helpers ----------

    private static readonly Type ControllerType = typeof(EnrolmentsController);

    private static IEnumerable<MethodInfo> Actions(string name) =>
        ControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                      .Where(m => m.Name == name);

    private static string? RolesOn(MethodInfo method) =>
        method.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

    private static EnrolViewModel InvokeBuildViewModel(Event ev, int? selectedCategoryId)
    {
        var method = ControllerType.GetMethod("BuildViewModel",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (EnrolViewModel)method.Invoke(null, new object?[] { ev, selectedCategoryId })!;
    }

    // ---------- Authorization rules ----------

    [Fact]
    public void Controller_RequiresLogin()
    {
        Assert.NotNull(ControllerType.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Create_BothOverloads_AreParticipantOnly()
    {
        var creates = Actions("Create").ToList();

        Assert.Equal(2, creates.Count);   // GET and POST
        Assert.All(creates, m => Assert.Equal("Participant", RolesOn(m)));
    }

    [Fact]
    public void My_IsParticipantOnly()
    {
        Assert.Equal("Participant", RolesOn(Actions("My").Single()));
    }

    [Fact]
    public void Withdraw_IsParticipantOnly()
    {
        Assert.Equal("Participant", RolesOn(Actions("Withdraw").Single()));
    }

    [Fact]
    public void ForEvent_IsForOrganisersAndAdmins()
    {
        var roles = RolesOn(Actions("ForEvent").Single());

        Assert.NotNull(roles);
        Assert.Contains("Organiser", roles!.Split(','));
        Assert.DoesNotContain("Participant", roles.Split(','));
    }

    [Fact]
    public void Withdraw_IsPostOnly()
    {
        Assert.NotNull(Actions("Withdraw").Single().GetCustomAttribute<HttpPostAttribute>());
    }

    [Fact]
    public void EveryPostAction_HasAntiForgeryProtection()
    {
        var postActions = ControllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null)
            .ToList();

        Assert.NotEmpty(postActions);
        Assert.All(postActions, m =>
            Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }

    // ---------- BuildViewModel ----------

    [Fact]
    public void BuildViewModel_CopiesEventDetails()
    {
        var ev = new Event
        {
            Id = 7,
            Name = "Durban 10km",
            Location = "Beachfront",
            Date = new DateTime(2026, 12, 5, 6, 30, 0),
            Categories = new List<Category>()
        };

        var model = InvokeBuildViewModel(ev, null);

        Assert.Equal(7, model.EventId);
        Assert.Equal("Durban 10km", model.EventName);
        Assert.Equal("Beachfront", model.Location);
        Assert.Equal(new DateTime(2026, 12, 5, 6, 30, 0), model.EventDate);
    }

    [Fact]
    public void BuildViewModel_PassesThroughSelectedCategoryId()
    {
        var ev = new Event { Categories = new List<Category>() };

        Assert.Equal(42, InvokeBuildViewModel(ev, 42).CategoryId);
        Assert.Null(InvokeBuildViewModel(ev, null).CategoryId);
    }

    [Fact]
    public void BuildViewModel_ListsOneDropdownItemPerCategoryWithIdAsValue()
    {
        var ev = new Event
        {
            Categories = new List<Category>
            {
                new() { Id = 1, Name = "Senior" },
                new() { Id = 2, Name = "Junior" }
            }
        };

        var items = InvokeBuildViewModel(ev, null).Categories.ToList();

        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Value == "1" && i.Text.StartsWith("Senior"));
        Assert.Contains(items, i => i.Value == "2" && i.Text.StartsWith("Junior"));
    }

    [Fact]
    public void BuildViewModel_SortsCategoriesByNameWithinTheSameKind()
    {
        var ev = new Event
        {
            Categories = new List<Category>
            {
                new() { Id = 1, Name = "Senior" },
                new() { Id = 2, Name = "Junior" },
                new() { Id = 3, Name = "Masters" }
            }
        };

        var texts = InvokeBuildViewModel(ev, null).Categories.Select(i => i.Text).ToList();

        Assert.StartsWith("Junior", texts[0]);
        Assert.StartsWith("Masters", texts[1]);
        Assert.StartsWith("Senior", texts[2]);
    }
}