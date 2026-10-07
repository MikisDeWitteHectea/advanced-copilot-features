using EventRegistration.API.Data;
using EventRegistration.API.Models.DTOs;
using EventRegistration.API.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.API.Controllers;

[ApiController]
[Route("api")]
public class CancellationPolicyController : ControllerBase
{
    private readonly AppDbContext _context;

    public CancellationPolicyController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("events/{eventId}/cancellation-policy")]
    public async Task<ActionResult<CancellationPolicyResponse>> CreateCancellationPolicy(int eventId, [FromBody] CreateCancellationPolicyRequest request)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        var policy = new CancellationPolicy
        {
            EventId = eventId,
            FullRefundDeadlineDays = request.FullRefundDeadlineDays,
            PartialRefundDeadlineDays = request.PartialRefundDeadlineDays,
            PartialRefundPercentage = request.PartialRefundPercentage,
            NoRefundAfterDays = request.NoRefundAfterDays,
            CancellationFee = request.CancellationFee
        };

        _context.CancellationPolicies.Add(policy);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCancellationPolicy), new { eventId }, MapToResponse(policy));
    }

    [HttpGet("events/{eventId}/cancellation-policy")]
    public async Task<ActionResult<CancellationPolicyResponse>> GetCancellationPolicy(int eventId)
    {
        var policy = await _context.CancellationPolicies
            .FirstOrDefaultAsync(p => p.EventId == eventId);

        if (policy == null)
            return NotFound();

        return Ok(MapToResponse(policy));
    }

    [HttpPut("events/{eventId}/cancellation-policy")]
    public async Task<ActionResult<CancellationPolicyResponse>> UpdateCancellationPolicy(int eventId, [FromBody] CreateCancellationPolicyRequest request)
    {
        var policy = await _context.CancellationPolicies
            .FirstOrDefaultAsync(p => p.EventId == eventId);

        if (policy == null)
            return NotFound();

        policy.FullRefundDeadlineDays = request.FullRefundDeadlineDays;
        policy.PartialRefundDeadlineDays = request.PartialRefundDeadlineDays;
        policy.PartialRefundPercentage = request.PartialRefundPercentage;
        policy.NoRefundAfterDays = request.NoRefundAfterDays;
        policy.CancellationFee = request.CancellationFee;

        await _context.SaveChangesAsync();

        return Ok(MapToResponse(policy));
    }

    [HttpPost("registrations/{id}/cancel")]
    public async Task<ActionResult<CancelRegistrationResponse>> CancelRegistrationWithRefund(int id)
    {
        var registration = await _context.Registrations
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (registration == null)
            return NotFound();

        if (registration.Status == "Cancelled")
            return BadRequest("Registration is already cancelled");

        var policy = await _context.CancellationPolicies
            .FirstOrDefaultAsync(p => p.EventId == registration.EventId);

        decimal refundAmount = 0;
        string refundReason = "";

        if (policy != null)
        {
            var daysUntilEvent = (registration.Event.StartDate - DateTime.UtcNow).TotalDays;

            if (daysUntilEvent >= policy.FullRefundDeadlineDays)
            {
                // Full refund
                refundAmount = registration.TotalAmount - (policy.CancellationFee ?? 0);
                refundReason = "full refund - cancelled well in advance";
            }
            else if (daysUntilEvent >= policy.PartialRefundDeadlineDays)
            {
                // Partial refund
                refundAmount = (registration.TotalAmount - (policy.CancellationFee ?? 0)) * (policy.PartialRefundPercentage / 100m);
                refundReason = $"partial refund - {policy.PartialRefundPercentage}% refund";
            }
            else if (daysUntilEvent >= policy.NoRefundAfterDays)
            {
                // No refund
                refundAmount = 0;
                refundReason = "no refund - too close to event date";
            }
            else
            {
                // No refund
                refundAmount = 0;
                refundReason = "no refund - past no-refund deadline";
            }
        }
        else
        {
            // Default policy: full refund
            refundAmount = registration.TotalAmount;
            refundReason = "Full refund - default policy";
        }

        refundAmount = Math.Max(0, refundAmount);

        registration.Status = "Cancelled";
        await _context.SaveChangesAsync();

        // Promote from waitlist
        await PromoteFromWaitlist(registration.EventId, registration.TicketTypeId);

        return Ok(new CancelRegistrationResponse(
            registration.Id,
            "Cancelled",
            refundAmount,
            refundReason
        ));
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

        // Update waitlisted registration to confirmed
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

    private static CancellationPolicyResponse MapToResponse(CancellationPolicy p) => new(
        p.Id,
        p.EventId,
        p.FullRefundDeadlineDays,
        p.PartialRefundDeadlineDays,
        p.PartialRefundPercentage,
        p.NoRefundAfterDays,
        p.CancellationFee
    );
}
