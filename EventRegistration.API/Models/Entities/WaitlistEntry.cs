namespace EventRegistration.API.Models.Entities;

public class WaitlistEntry
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int TicketTypeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int Position { get; set; }
    public DateTime JoinedDate { get; set; }
    public DateTime? PromotionExpiry { get; set; }
    public string? DiscountCode { get; set; }

    // Navigation properties
    public Event Event { get; set; } = null!;
    public TicketType TicketType { get; set; } = null!;
}
