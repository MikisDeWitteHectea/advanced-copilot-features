using EventRegistration.API.Data;
using EventRegistration.API.Models.DTOs;
using EventRegistration.API.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EventRegistration.API.Controllers;

[ApiController]
[Route("api")]
public class DiscountCodesController : ControllerBase
{
    private readonly AppDbContext _context;

    public DiscountCodesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("events/{eventId}/discount-codes")]
    public async Task<ActionResult<DiscountCodeResponse>> CreateDiscountCode(int eventId, [FromBody] CreateDiscountCodeRequest request)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound();

        // Validation
        if (request.DiscountType == "Percentage" && (request.DiscountValue < 0 || request.DiscountValue > 100))
            return BadRequest("Percentage discount must be between 0 and 100");

        // Check for duplicate code
        var existing = await _context.DiscountCodes
            .FirstOrDefaultAsync(d => d.EventId == eventId && d.Code.ToUpper() == request.Code.ToUpper());
        if (existing != null)
            return BadRequest("Discount code already exists for this event");

        var discountCode = new DiscountCode
        {
            EventId = eventId,
            Code = request.Code,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MaxUses = request.MaxUses,
            CurrentUses = 0,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            ApplicableTicketTypeIds = request.ApplicableTicketTypeIds != null
                ? JsonSerializer.Serialize(request.ApplicableTicketTypeIds)
                : string.Empty,
            Status = "Active"
        };

        _context.DiscountCodes.Add(discountCode);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDiscountCodeByCode), new { code = discountCode.Code }, MapToResponse(discountCode));
    }

    [HttpGet("events/{eventId}/discount-codes")]
    public async Task<ActionResult<List<DiscountCodeResponse>>> GetAllDiscountCodes(int eventId)
    {
        var codes = await _context.DiscountCodes
            .Where(d => d.EventId == eventId)
            .ToListAsync();

        return Ok(codes.Select(MapToResponse).ToList());
    }

    [HttpGet("discount-codes/{code}")]
    public async Task<ActionResult<DiscountCodeResponse>> GetDiscountCodeByCode(string code)
    {
        var discountCode = await _context.DiscountCodes
            .FirstOrDefaultAsync(d => d.Code.ToUpper() == code.ToUpper());

        if (discountCode == null)
            return NotFound();

        return Ok(MapToResponse(discountCode));
    }

    [HttpPut("discount-codes/{id}")]
    public async Task<ActionResult<DiscountCodeResponse>> UpdateDiscountCode(int id, [FromBody] UpdateDiscountCodeRequest request)
    {
        var discountCode = await _context.DiscountCodes.FindAsync(id);
        if (discountCode == null)
            return NotFound();

        // Validation
        if (request.DiscountType == "Percentage" && (request.DiscountValue < 0 || request.DiscountValue > 100))
            return BadRequest("Percentage discount must be between 0 and 100");

        discountCode.Code = request.Code;
        discountCode.DiscountType = request.DiscountType;
        discountCode.DiscountValue = request.DiscountValue;
        discountCode.MaxUses = request.MaxUses;
        discountCode.ValidFrom = request.ValidFrom;
        discountCode.ValidUntil = request.ValidUntil;
        discountCode.ApplicableTicketTypeIds = request.ApplicableTicketTypeIds != null
            ? JsonSerializer.Serialize(request.ApplicableTicketTypeIds)
            : string.Empty;
        discountCode.Status = request.Status;

        await _context.SaveChangesAsync();

        return Ok(MapToResponse(discountCode));
    }

    [HttpDelete("discount-codes/{id}")]
    public async Task<IActionResult> DeleteDiscountCode(int id)
    {
        var discountCode = await _context.DiscountCodes.FindAsync(id);
        if (discountCode == null)
            return NotFound();

        // Check if it has been used
        if (discountCode.CurrentUses > 0)
            return BadRequest("Cannot delete discount code that has been used");

        _context.DiscountCodes.Remove(discountCode);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("discount-codes/{code}/validate")]
    public async Task<ActionResult<ValidateDiscountCodeResponse>> ValidateDiscountCode(string code, [FromBody] ValidateDiscountCodeRequest request)
    {
        var discountCode = await _context.DiscountCodes
            .FirstOrDefaultAsync(d => d.Code.ToUpper() == code.ToUpper());

        if (discountCode == null)
            return Ok(new ValidateDiscountCodeResponse(false, "Invalid discount code", null));

        if (discountCode.Status != "Active")
            return Ok(new ValidateDiscountCodeResponse(false, "Discount code is not active", null));

        if (DateTime.UtcNow < discountCode.ValidFrom || DateTime.UtcNow > discountCode.ValidUntil)
            return Ok(new ValidateDiscountCodeResponse(false, "Discount code is not valid at this time", null));

        if (discountCode.MaxUses.HasValue && discountCode.CurrentUses >= discountCode.MaxUses.Value)
            return Ok(new ValidateDiscountCodeResponse(false, "Discount code has reached its maximum uses", null));

        // Check applicable ticket types
        if (!string.IsNullOrWhiteSpace(discountCode.ApplicableTicketTypeIds))
        {
            var applicableIds = JsonSerializer.Deserialize<List<int>>(discountCode.ApplicableTicketTypeIds);
            if (applicableIds != null && applicableIds.Count > 0 && !applicableIds.Contains(request.TicketTypeId))
                return Ok(new ValidateDiscountCodeResponse(false, "Discount code is not applicable to this ticket type", null));
        }

        // Calculate discount amount
        var ticketType = await _context.TicketTypes.FindAsync(request.TicketTypeId);
        if (ticketType == null)
            return Ok(new ValidateDiscountCodeResponse(false, "Invalid ticket type", null));

        decimal discountAmount = 0;
        if (discountCode.DiscountType == "Percentage")
        {
            discountAmount = ticketType.Price * (discountCode.DiscountValue / 100);
        }
        else if (discountCode.DiscountType == "FixedAmount")
        {
            discountAmount = Math.Min(discountCode.DiscountValue, ticketType.Price);
        }

        return Ok(new ValidateDiscountCodeResponse(true, null, discountAmount));
    }

    private static DiscountCodeResponse MapToResponse(DiscountCode d)
    {
        List<int>? applicableTicketTypeIds = null;
        if (!string.IsNullOrWhiteSpace(d.ApplicableTicketTypeIds))
        {
            applicableTicketTypeIds = JsonSerializer.Deserialize<List<int>>(d.ApplicableTicketTypeIds);
        }

        return new DiscountCodeResponse(
            d.Id,
            d.EventId,
            d.Code,
            d.DiscountType,
            d.DiscountValue,
            d.MaxUses,
            d.CurrentUses,
            d.ValidFrom,
            d.ValidUntil,
            applicableTicketTypeIds,
            d.Status
        );
    }
}
