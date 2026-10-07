namespace EventRegistration.API.Models.Entities;

public class DiscountCode
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty; // Percentage, FixedAmount
    public decimal DiscountValue { get; set; }
    public int? MaxUses { get; set; }
    public int CurrentUses { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public string ApplicableTicketTypeIds { get; set; } = string.Empty; // JSON array
    public string Status { get; set; } = "Active"; // Active, Inactive, Expired

    // Navigation properties
    public Event Event { get; set; } = null!;
}
