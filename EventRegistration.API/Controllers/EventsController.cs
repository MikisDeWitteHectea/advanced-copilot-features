using EventRegistration.API.Data;
using EventRegistration.API.Models.DTOs;
using EventRegistration.API.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.API.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _context;

    public EventsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<EventResponse>> CreateEvent([FromBody] CreateEventRequest request)
    {
        // Validation
        if (request.EndDate <= request.StartDate)
            return BadRequest("End date must be after start date");

        if (request.RegistrationDeadline.HasValue && request.RegistrationDeadline.Value >= request.StartDate)
            return BadRequest("Registration deadline must be before event start date");

        if (request.OverallCapacity <= 0)
            return BadRequest("Overall capacity must be positive");

        var ev = new Event
        {
            Name = request.Name,
            Description = request.Description,
            VenueName = request.VenueName,
            VenueAddress = request.VenueAddress,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            OverallCapacity = request.OverallCapacity,
            RegistrationDeadline = request.RegistrationDeadline,
            Status = request.Status
        };

        _context.Events.Add(ev);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEventById), new { id = ev.Id }, MapToResponse(ev));
    }

    [HttpGet]
    public async Task<ActionResult<List<EventResponse>>> GetAllEvents([FromQuery] string? status = null)
    {
        var query = _context.Events.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(e => e.Status == status);
        }

        var events = await query.ToListAsync();
        return Ok(events.Select(MapToResponse).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EventResponse>> GetEventById(int id)
    {
        var ev = await _context.Events.FindAsync(id);
        if (ev == null)
            return NotFound();

        return Ok(MapToResponse(ev));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EventResponse>> UpdateEvent(int id, [FromBody] UpdateEventRequest request)
    {
        var ev = await _context.Events.FindAsync(id);
        if (ev == null)
            return NotFound();

        // Validation
        if (request.EndDate <= request.StartDate)
            return BadRequest("End date must be after start date");

        if (request.RegistrationDeadline.HasValue && request.RegistrationDeadline.Value >= request.StartDate)
            return BadRequest("Registration deadline must be before event start date");

        if (request.OverallCapacity <= 0)
            return BadRequest("Overall capacity must be positive");

        // Check if dates can be modified
        var hasRegistrations = await _context.Registrations.AnyAsync(r => r.EventId == id);
        if (hasRegistrations && (ev.StartDate != request.StartDate || ev.EndDate != request.EndDate))
            return BadRequest("Cannot modify event dates when registrations exist");

        ev.Name = request.Name;
        ev.Description = request.Description;
        ev.VenueName = request.VenueName;
        ev.VenueAddress = request.VenueAddress;
        ev.StartDate = request.StartDate;
        ev.EndDate = request.EndDate;
        ev.OverallCapacity = request.OverallCapacity;
        ev.RegistrationDeadline = request.RegistrationDeadline;
        ev.Status = request.Status;

        await _context.SaveChangesAsync();

        return Ok(MapToResponse(ev));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _context.Events.FindAsync(id);
        if (ev == null)
            return NotFound();

        // Check if there are registrations
        var hasRegistrations = await _context.Registrations.AnyAsync(r => r.EventId == id);
        if (hasRegistrations)
            return BadRequest("Cannot delete event with existing registrations");

        _context.Events.Remove(ev);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static EventResponse MapToResponse(Event ev) => new(
        ev.Id,
        ev.Name,
        ev.Description,
        ev.VenueName,
        ev.VenueAddress,
        ev.StartDate,
        ev.EndDate,
        ev.OverallCapacity,
        ev.RegistrationDeadline,
        ev.Status
    );
}
