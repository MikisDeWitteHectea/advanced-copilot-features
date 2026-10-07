namespace EventRegistration.API.Models.Entities;

public class Registration
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int TicketTypeId { get; set; }
    public DateTime RegistrationDate { get; set; }
    public string Status { get; set; } = "Confirmed"; // Confirmed, Cancelled, Waitlisted, Refunded
    public decimal TotalAmount { get; set; }
    public string? DiscountCodeUsed { get; set; }

    // Navigation properties
    public Event Event { get; set; } = null!;
    public TicketType TicketType { get; set; } = null!;
}
