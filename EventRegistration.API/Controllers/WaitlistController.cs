using EventRegistration.API.Data;
using EventRegistration.API.Models.DTOs;
using EventRegistration.API.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.API.Controllers;

[ApiController]
[Route("api")]
public class WaitlistController : ControllerBase
{
    private readonly AppDbContext _context;

    public WaitlistController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("events/{eventId}/waitlist")]
    public async Task<ActionResult<List<WaitlistEntryResponse>>> GetWaitlist(int eventId)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        var entries = await _context.WaitlistEntries
            .Where(w => w.EventId == eventId)
            .OrderBy(w => w.Position)
            .ToListAsync();

        return Ok(entries.Select(MapToResponse).ToList());
    }

    [HttpGet("events/{eventId}/waitlist/{ticketTypeId}")]
    public async Task<ActionResult<List<WaitlistEntryResponse>>> GetWaitlistForTicketType(int eventId, int ticketTypeId)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        var entries = await _context.WaitlistEntries
            .Where(w => w.EventId == eventId && w.TicketTypeId == ticketTypeId)
            .OrderBy(w => w.Position)
            .ToListAsync();

        return Ok(entries.Select(MapToResponse).ToList());
    }

    [HttpPost("waitlist/{waitlistId}/confirm")]
    public async Task<IActionResult> ConfirmWaitlistPromotion(int waitlistId)
    {
        var entry = await _context.WaitlistEntries.FindAsync(waitlistId);
        if (entry == null)
            return NotFound();

        // Remove from waitlist and update registration to confirmed
        var registration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.EventId == entry.EventId && r.Email == entry.Email && r.Status == "Waitlisted");

        if (registration != null)
        {
            registration.Status = "Confirmed";
            registration.RegistrationDate = DateTime.UtcNow;
        }

        _context.WaitlistEntries.Remove(entry);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("waitlist/{waitlistId}")]
    public async Task<IActionResult> RemoveFromWaitlist(int waitlistId)
    {
        var entry = await _context.WaitlistEntries.FindAsync(waitlistId);
        if (entry == null)
            return NotFound();

        _context.WaitlistEntries.Remove(entry);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static WaitlistEntryResponse MapToResponse(WaitlistEntry w) => new(
        w.Id,
        w.EventId,
        w.TicketTypeId,
        w.FirstName,
        w.LastName,
        w.Email,
        w.Position,
        w.JoinedDate,
        w.PromotionExpiry
    );
}
