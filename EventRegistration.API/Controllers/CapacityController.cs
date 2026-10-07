using EventRegistration.API.Data;
using EventRegistration.API.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.API.Controllers;

[ApiController]
[Route("api/events")]
public class CapacityController : ControllerBase
{
    private readonly AppDbContext _context;

    public CapacityController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{eventId}/capacity")]
    public async Task<ActionResult<CapacityResponse>> GetCapacity(int eventId)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        var ticketTypes = await _context.TicketTypes
            .Where(t => t.EventId == eventId)
            .ToListAsync();

        var overallRegistered = await _context.Registrations
            .CountAsync(r => r.EventId == eventId && r.Status == "Confirmed");

        var ticketTypeCapacities = new List<TicketTypeCapacity>();

        foreach (var tt in ticketTypes)
        {
            var registered = await _context.Registrations
                .CountAsync(r => r.TicketTypeId == tt.Id && r.Status == "Confirmed");

            ticketTypeCapacities.Add(new TicketTypeCapacity(
                tt.Id,
                tt.Name,
                tt.Capacity,
                registered,
                tt.Capacity - registered
            ));
        }

        return Ok(new CapacityResponse(
            eventId,
            ev.OverallCapacity,
            overallRegistered,
            ev.OverallCapacity - overallRegistered,
            ticketTypeCapacities
        ));
    }
}
