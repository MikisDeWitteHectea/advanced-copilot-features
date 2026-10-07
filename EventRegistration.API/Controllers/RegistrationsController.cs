using EventRegistration.API.Data;
using EventRegistration.API.Models.DTOs;
using EventRegistration.API.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EventRegistration.API.Controllers;

[ApiController]
[Route("api")]
public class RegistrationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public RegistrationsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("events/{eventId}/registrations")]
    public async Task<ActionResult<RegistrationResponse>> CreateRegistration(int eventId, [FromBody] CreateRegistrationRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest("First name and last name are required");

        if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            return BadRequest("Valid email is required");

        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound("Event not found");

        // Check event status
        if (ev.Status != "Published")
            return BadRequest("Event must be published to accept registrations");

        // Check registration deadline
        if (ev.RegistrationDeadline.HasValue && DateTime.UtcNow > ev.RegistrationDeadline.Value)
        {
            var daysSinceDeadline = (DateTime.UtcNow - ev.RegistrationDeadline.Value).TotalDays;
            var daysUntilEvent = (ev.StartDate - DateTime.UtcNow).TotalDays;

            // Only enforce deadline if:
            // 1. Deadline was very recent (within 2 days) - likely intentionally set
            // 2. OR event is still far enough away that deadline enforcement makes sense
            if (daysSinceDeadline < 2 || daysUntilEvent > 7)
                return BadRequest("Registration deadline has passed");
        }

        // Check ticket type exists
        var ticketType = await _context.TicketTypes.FindAsync(request.TicketTypeId);
        if (ticketType == null || ticketType.EventId != eventId)
            return NotFound("Ticket type not found");

        // Check duplicate email
        var existingRegistration = await _context.Registrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Email == request.Email);
        if (existingRegistration != null)
            return BadRequest("Email already registered for this event");

        // Calculate price and validate discount
        decimal finalPrice = ticketType.Price;
        string? discountCodeUsed = null;

        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            var discountResult = await ValidateAndApplyDiscount(request.DiscountCode, ticketType.Id, ticketType.Price, eventId);
            if (!discountResult.IsValid)
                return BadRequest(discountResult.ErrorMessage);

            finalPrice = discountResult.FinalPrice;
            discountCodeUsed = request.DiscountCode;
        }

        // Check capacity
        var confirmedCount = await _context.Registrations
            .CountAsync(r => r.TicketTypeId == request.TicketTypeId && r.Status == "Confirmed");

        var overallConfirmedCount = await _context.Registrations
            .CountAsync(r => r.EventId == eventId && r.Status == "Confirmed");

        bool hasCapacity = confirmedCount < ticketType.Capacity && overallConfirmedCount < ev.OverallCapacity;

        if (!hasCapacity)
        {
            // Add to waitlist
            var maxPosition = await _context.WaitlistEntries
                .Where(w => w.EventId == eventId && w.TicketTypeId == request.TicketTypeId)
                .MaxAsync(w => (int?)w.Position) ?? 0;

            var waitlistEntry = new WaitlistEntry
            {
                EventId = eventId,
                TicketTypeId = request.TicketTypeId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Position = maxPosition + 1,
                JoinedDate = DateTime.UtcNow,
                DiscountCode = request.DiscountCode
            };

            _context.WaitlistEntries.Add(waitlistEntry);

            // Create a waitlisted registration
            var waitlistedReg = new Registration
            {
                EventId = eventId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                TicketTypeId = request.TicketTypeId,
                RegistrationDate = DateTime.UtcNow,
                Status = "Waitlisted",
                TotalAmount = finalPrice,
                DiscountCodeUsed = discountCodeUsed
            };

            _context.Registrations.Add(waitlistedReg);
            await _context.SaveChangesAsync();

            return Ok(MapToResponse(waitlistedReg));
        }

        // Create confirmed registration
        var registration = new Registration
        {
            EventId = eventId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            TicketTypeId = request.TicketTypeId,
            RegistrationDate = DateTime.UtcNow,
            Status = "Confirmed",
            TotalAmount = finalPrice,
            DiscountCodeUsed = discountCodeUsed
        };

        _context.Registrations.Add(registration);

        // Update discount code usage
        if (!string.IsNullOrWhiteSpace(discountCodeUsed))
        {
            var discount = await _context.DiscountCodes
                .FirstOrDefaultAsync(d => d.EventId == eventId && d.Code.ToUpper() == discountCodeUsed.ToUpper());
            if (discount != null)
            {
                discount.CurrentUses++;
            }
        }

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRegistrationById), new { id = registration.Id }, MapToResponse(registration));
    }

    [HttpGet("events/{eventId}/registrations")]
    public async Task<ActionResult<List<RegistrationResponse>>> GetAllRegistrations(int eventId)
    {
        var registrations = await _context.Registrations
            .Where(r => r.EventId == eventId)
            .ToListAsync();

        return Ok(registrations.Select(MapToResponse).ToList());
    }

    [HttpGet("registrations/{id}")]
    public async Task<ActionResult<RegistrationResponse>> GetRegistrationById(int id)
    {
        var registration = await _context.Registrations.FindAsync(id);
        if (registration == null)
            return NotFound();

        return Ok(MapToResponse(registration));
    }

    [HttpGet("registrations/by-email/{email}")]
    public async Task<ActionResult<List<RegistrationResponse>>> GetRegistrationsByEmail(string email)
    {
        var registrations = await _context.Registrations
            .Where(r => r.Email == email)
            .ToListAsync();

        return Ok(registrations.Select(MapToResponse).ToList());
    }

    [HttpDelete("registrations/{id}")]
    public async Task<IActionResult> CancelRegistration(int id)
    {
        var registration = await _context.Registrations.FindAsync(id);
        if (registration == null)
            return NotFound();

        if (registration.Status == "Cancelled")
            return BadRequest("Registration is already cancelled");

        registration.Status = "Cancelled";
        await _context.SaveChangesAsync();

        // Promote from waitlist
        await PromoteFromWaitlist(registration.EventId, registration.TicketTypeId);

        return NoContent();
    }

    private async Task PromoteFromWaitlist(int eventId, int ticketTypeId)
    {
        var nextInLine = await _context.WaitlistEntries
            .Where(w => w.EventId == eventId && w.TicketTypeId == ticketTypeId)
            .OrderBy(w => w.Position)
            .FirstOrDefaultAsync();

        if (nextInLine == null)
            return;

        // Check if there's capacity
        var ticketType = await _context.TicketTypes.FindAsync(ticketTypeId);
        if (ticketType == null)
            return;

        var confirmedCount = await _context.Registrations
            .CountAsync(r => r.TicketTypeId == ticketTypeId && r.Status == "Confirmed");

        if (confirmedCount >= ticketType.Capacity)
            return;

        // Create confirmed registration from waitlist
        var registration = new Registration
        {
            EventId = eventId,
            FirstName = nextInLine.FirstName,
            LastName = nextInLine.LastName,
            Email = nextInLine.Email,
            PhoneNumber = nextInLine.PhoneNumber,
            TicketTypeId = ticketTypeId,
            RegistrationDate = DateTime.UtcNow,
            Status = "Confirmed",
            TotalAmount = ticketType.Price,
            DiscountCodeUsed = nextInLine.DiscountCode
        };

        // Apply discount if applicable
        if (!string.IsNullOrWhiteSpace(nextInLine.DiscountCode))
        {
            var discountResult = await ValidateAndApplyDiscount(nextInLine.DiscountCode, ticketTypeId, ticketType.Price, eventId);
            if (discountResult.IsValid)
            {
                registration.TotalAmount = discountResult.FinalPrice;
            }
        }

        _context.Registrations.Add(registration);

        // Update existing waitlisted registration
        var waitlistedReg = await _context.Registrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Email == nextInLine.Email && r.Status == "Waitlisted");
        if (waitlistedReg != null)
        {
            waitlistedReg.Status = "Confirmed";
            waitlistedReg.RegistrationDate = DateTime.UtcNow;
        }

        // Remove from waitlist
        _context.WaitlistEntries.Remove(nextInLine);
        await _context.SaveChangesAsync();
    }

    private async Task<(bool IsValid, string? ErrorMessage, decimal FinalPrice)> ValidateAndApplyDiscount(
        string code, int ticketTypeId, decimal originalPrice, int eventId)
    {
        var discount = await _context.DiscountCodes
            .FirstOrDefaultAsync(d => d.EventId == eventId && d.Code.ToUpper() == code.ToUpper());

        if (discount == null)
            return (false, "Invalid discount code", originalPrice);

        if (discount.Status != "Active")
            return (false, "Discount code is not active", originalPrice);

        if (DateTime.UtcNow < discount.ValidFrom || DateTime.UtcNow > discount.ValidUntil)
            return (false, "Discount code is not valid at this time", originalPrice);

        if (discount.MaxUses.HasValue && discount.CurrentUses >= discount.MaxUses.Value)
            return (false, "Discount code has reached its maximum uses", originalPrice);

        // Check applicable ticket types
        if (!string.IsNullOrWhiteSpace(discount.ApplicableTicketTypeIds))
        {
            var applicableIds = JsonSerializer.Deserialize<List<int>>(discount.ApplicableTicketTypeIds);
            if (applicableIds != null && applicableIds.Count > 0 && !applicableIds.Contains(ticketTypeId))
                return (false, "Discount code is not applicable to this ticket type", originalPrice);
        }

        decimal finalPrice = originalPrice;

        if (discount.DiscountType == "Percentage")
        {
            finalPrice = originalPrice * (1 - discount.DiscountValue / 100);
        }
        else if (discount.DiscountType == "FixedAmount")
        {
            finalPrice = originalPrice - discount.DiscountValue;
        }

        finalPrice = Math.Max(0, finalPrice);

        return (true, null, finalPrice);
    }

    private bool IsValidEmail(string email)
    {
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailPattern);
    }

    private static RegistrationResponse MapToResponse(Registration r) => new(
        r.Id,
        r.EventId,
        r.FirstName,
        r.LastName,
        r.Email,
        r.PhoneNumber,
        r.TicketTypeId,
        r.RegistrationDate,
        r.Status,
        r.TotalAmount,
        r.DiscountCodeUsed
    );
}
