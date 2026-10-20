using RaceDay.Data;
using RaceDay.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace RaceDay.Controllers;

[Authorize]
public class EventsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public EventsController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    // Both roles
    public async Task<IActionResult> Index()
    {
        ViewBag.UserId = _users.GetUserId(User);
        var events = await _db.Events
            .Include(e => e.Organiser)
            .AsNoTracking()
            .OrderBy(e => e.Date)
            .ToListAsync();
        return View(events);
    }

    // Both roles
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();

        var userId = _users.GetUserId(User);
        ViewBag.IsOwner = ev.OrganiserId == userId;
        ViewBag.IsEnrolled = await _db.Enrolments
            .AnyAsync(en => en.EventId == ev.Id && en.ParticipantId == userId);

        return View(ev);
    }

    [Authorize(Roles = "Admin,Organiser")]
    public IActionResult Create()
    {
        return View(new Event { Date = DateTime.Today.AddDays(30).AddHours(8) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Organiser")]
    public async Task<IActionResult> Create([Bind("Name,Description,Date,Location,DistanceKm,EventType")] Event ev)
    {
        if (!ModelState.IsValid) return View(ev);

        ev.OrganiserId = _users.GetUserId(User)!;
        _db.Add(ev);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Event created. Now add the categories for it.";
        return RedirectToAction("Index", "Categories", new { eventId = ev.Id });
    }
    [Authorize(Roles = "Admin,Organiser")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var ev = await FindOwnedEventAsync(id.Value);
        return ev == null ? NotFound() : View(ev);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Organiser")]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await FindOwnedEventAsync(id);
        if (ev == null) return NotFound();

        // Whitelist bindable properties (prevents over-posting, keeps OrganiserId safe)
        var updated = await TryUpdateModelAsync(ev, "",
            e => e.Name, e => e.Description, e => e.Date,
            e => e.Location, e => e.DistanceKm, e => e.EventType);

        if (!updated) return View(ev);

        await _db.SaveChangesAsync();
        TempData["Success"] = "Event updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin,Organiser")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var ev = await FindOwnedEventAsync(id.Value);
        return ev == null ? NotFound() : View(ev);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Organiser")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var ev = await FindOwnedEventAsync(id);
        if (ev == null) return NotFound();

        if (await _db.Enrolments.AnyAsync(e => e.EventId == id))
        {
            TempData["Error"] = "This event has enrolments and cannot be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Events.Remove(ev);   // categories are cascade-deleted
        await _db.SaveChangesAsync();
        TempData["Success"] = "Event deleted.";
        return RedirectToAction(nameof(Index));
    }

    private Task<Event?> FindOwnedEventAsync(int id)
    {
        var userId = _users.GetUserId(User);
        return _db.Events.FirstOrDefaultAsync(e => e.Id == id && e.OrganiserId == userId);
    }
}
