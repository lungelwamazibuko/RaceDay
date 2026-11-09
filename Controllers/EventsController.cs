using RaceDay.Data;
using RaceDay.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using RaceDay.Services;

namespace RaceDay.Controllers;

[Authorize]
public class EventsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IBlobStorageService _blobs;


    public EventsController(ApplicationDbContext db, UserManager<ApplicationUser> users, IBlobStorageService blobs)
    { 
        _db = db;
        _users = users;
        _blobs = blobs;
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
        if (id == null)
        {
            return NotFound();
        }

        var ev = await _db.Events
            .AsNoTracking()
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .SingleOrDefaultAsync(e => e.Id == id.Value);

        if (ev == null)
        {
            return NotFound();
        }

        ViewBag.IsOwner = ev.OrganiserId == _users.GetUserId(User);
        ViewBag.IsEnrolled = await _db.Enrolments
            .AnyAsync(en =>
                en.EventId == ev.Id &&
                en.ParticipantId == _users.GetUserId(User));

        return View(ev);
    }

    [Authorize(Roles = "Admin,Organiser")]
    public IActionResult Create()
    {
        return View(new Event { Date = DateTime.Today.AddDays(30).AddHours(8) });
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(5_000_000)]
    [Authorize(Roles = "Admin,Organiser")]
    public async Task<IActionResult> Create([Bind("Name,Description,Date,Location,DistanceKm,EventType")] Event ev, IFormFile? bannerFile)
    {
        var hasBanner = bannerFile is { Length: > 0 };
        if (hasBanner)
        {
            var error = _blobs.Validate(bannerFile!);
            if (error != null) ModelState.AddModelError("bannerFile", error);
        }
        if (!ModelState.IsValid) return View(ev);

        ev.OrganiserId = _users.GetUserId(User)!;
        BlobUploadResult? uploaded = null;

        try
        {
            if (hasBanner)
            {
                uploaded = await _blobs.UploadBannerAsync(bannerFile!);
                ev.BannerBlobName = uploaded.BlobName;
                ev.BannerImageUrl = uploaded.Url;
            }
            _db.Add(ev);
            await _db.SaveChangesAsync();
        }
        catch
        {
            if (uploaded != null) await _blobs.DeleteAsync(uploaded.BlobName);
            throw;
        }
        TempData["Success"] = "Event created.";

        return RedirectToAction(
            nameof(CategoriesController.Index),
            "Categories",
            new { eventId = ev.Id });
    }

    [Authorize(Roles = "Admin,Organiser")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var ev = await FindOwnedEventAsync(id.Value);
        return ev == null ? NotFound() : View(ev);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(5_000_000)]
    [Authorize(Roles = "Admin,Organiser")]
    public async Task<IActionResult> Edit(int id, IFormFile? bannerFile, bool removeBanner)
    {
        var ev = await FindOwnedEventAsync(id);
        if (ev == null) return NotFound();

        var hasBanner = bannerFile is { Length: > 0 };
        if (hasBanner)
        {
            var error = _blobs.Validate(bannerFile!);
            if (error != null) ModelState.AddModelError("bannerFile", error);
        }

        // Whitelist bindable properties (prevents over-posting, keeps OrganiserId safe)
        var updated = await TryUpdateModelAsync(ev, "",
            e => e.Name, e => e.Description, e => e.Date,
            e => e.Location, e => e.DistanceKm, e => e.EventType);

        if (!updated || !ModelState.IsValid) return View(ev);
        
        var oldBanner = ev.BannerBlobName;
        BlobUploadResult? uploaded = null;

        try
        {
            if (hasBanner)
            {
                uploaded = await _blobs.UploadBannerAsync(bannerFile!);
                ev.BannerBlobName = uploaded.BlobName;
                ev.BannerImageUrl = uploaded.Url;
            }
            else if (removeBanner)
            {
                ev.BannerBlobName = null;
                ev.BannerImageUrl = null;

            }
            await _db.SaveChangesAsync();
        }
        catch
        {
            if (uploaded != null) await _blobs.DeleteAsync(uploaded.BlobName);
            throw;
        }

        if ((uploaded != null || removeBanner) && oldBanner != null)
        {
            await _blobs.DeleteAsync(oldBanner);
        }

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

        var blobName = ev.BannerBlobName;
        _db.Events.Remove(ev);   // categories are cascade-deleted
        await _db.SaveChangesAsync();
        await _blobs.DeleteAsync(blobName);
        TempData["Success"] = "Event deleted.";
        return RedirectToAction(nameof(Index));
    }

    private Task<Event?> FindOwnedEventAsync(int id)
    {
        var userId = _users.GetUserId(User);
        return _db.Events.FirstOrDefaultAsync(e => e.Id == id && e.OrganiserId == userId);
    }
}
