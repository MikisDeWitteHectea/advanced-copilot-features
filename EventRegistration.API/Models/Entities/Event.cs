namespace EventRegistration.API.Models.Entities;

public class Event
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? VenueName { get; set; }
    public string? VenueAddress { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int OverallCapacity { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Published, Cancelled, Completed

    // Navigation properties
    public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    public ICollection<DiscountCode> DiscountCodes { get; set; } = new List<DiscountCode>();
    public CancellationPolicy? CancellationPolicy { get; set; }
}
