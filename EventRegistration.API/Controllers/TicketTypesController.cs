using EventRegistration.API.Data;
using EventRegistration.API.Models.DTOs;
using EventRegistration.API.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.API.Controllers;

[ApiController]
[Route("api/events/{eventId}/ticket-types")]
public class TicketTypesController : ControllerBase
{
    private readonly AppDbContext _context;

    public TicketTypesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<TicketTypeResponse>> CreateTicketType(int eventId, [FromBody] CreateTicketTypeRequest request)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        // Validation
        if (request.Price < 0)
            return BadRequest("Price cannot be negative");

        if (request.Capacity <= 0)
            return BadRequest("Capacity must be positive");

        // Check total capacity
        var existingCapacity = await _context.TicketTypes
            .Where(t => t.EventId == eventId)
            .SumAsync(t => t.Capacity);

        if (existingCapacity + request.Capacity > ev.OverallCapacity)
            return BadRequest("Sum of ticket type capacities cannot exceed event capacity");

        // Check date ranges
        if (request.AvailableFrom.HasValue && request.AvailableUntil.HasValue)
        {
            if (request.AvailableUntil.Value > ev.StartDate)
                return BadRequest("Available until date must be before or at event start");
        }

        var ticketType = new TicketType
        {
            EventId = eventId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Capacity = request.Capacity,
            AvailableFrom = request.AvailableFrom,
            AvailableUntil = request.AvailableUntil
        };

        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTicketTypeById), new { eventId, id = ticketType.Id }, await MapToResponse(ticketType));
    }

    [HttpGet]
    public async Task<ActionResult<List<TicketTypeResponse>>> GetAllTicketTypes(int eventId)
    {
        var ticketTypes = await _context.TicketTypes
            .Where(t => t.EventId == eventId)
            .ToListAsync();

        var responses = new List<TicketTypeResponse>();
        foreach (var tt in ticketTypes)
        {
            responses.Add(await MapToResponse(tt));
        }

        return Ok(responses);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TicketTypeResponse>> GetTicketTypeById(int eventId, int id)
    {
        var ticketType = await _context.TicketTypes
            .FirstOrDefaultAsync(t => t.Id == id && t.EventId == eventId);

        if (ticketType == null)
            return NotFound();

        return Ok(await MapToResponse(ticketType));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TicketTypeResponse>> UpdateTicketType(int eventId, int id, [FromBody] UpdateTicketTypeRequest request)
    {
        var ticketType = await _context.TicketTypes
            .FirstOrDefaultAsync(t => t.Id == id && t.EventId == eventId);

        if (ticketType == null)
            return NotFound();

        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        // Validation
        if (request.Price < 0)
            return BadRequest("Price cannot be negative");

        if (request.Capacity <= 0)
            return BadRequest("Capacity must be positive");

        // Check if capacity can be reduced
        var registeredCount = await _context.Registrations
            .CountAsync(r => r.TicketTypeId == id && r.Status == "Confirmed");

        if (request.Capacity < registeredCount)
            return BadRequest("Cannot reduce capacity below current registration count");

        // Check total capacity
        var existingCapacity = await _context.TicketTypes
            .Where(t => t.EventId == eventId && t.Id != id)
            .SumAsync(t => t.Capacity);

        if (existingCapacity + request.Capacity > ev.OverallCapacity)
            return BadRequest("Sum of ticket type capacities cannot exceed event capacity");

        ticketType.Name = request.Name;
        ticketType.Description = request.Description;
        ticketType.Price = request.Price;
        ticketType.Capacity = request.Capacity;
        ticketType.AvailableFrom = request.AvailableFrom;
        ticketType.AvailableUntil = request.AvailableUntil;

        await _context.SaveChangesAsync();

        return Ok(await MapToResponse(ticketType));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTicketType(int eventId, int id)
    {
        var ticketType = await _context.TicketTypes
            .FirstOrDefaultAsync(t => t.Id == id && t.EventId == eventId);

        if (ticketType == null)
            return NotFound();

        // Check if there are registrations
        var hasRegistrations = await _context.Registrations.AnyAsync(r => r.TicketTypeId == id);
        if (hasRegistrations)
            return BadRequest("Cannot delete ticket type with existing registrations");

        _context.TicketTypes.Remove(ticketType);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<TicketTypeResponse> MapToResponse(TicketType tt)
    {
        var registeredCount = await _context.Registrations
            .CountAsync(r => r.TicketTypeId == tt.Id && r.Status == "Confirmed");

        return new TicketTypeResponse(
            tt.Id,
            tt.EventId,
            tt.Name,
            tt.Description,
            tt.Price,
            tt.Capacity,
            tt.Capacity - registeredCount,
            tt.AvailableFrom,
            tt.AvailableUntil
        );
    }
}
