namespace EventRegistration.API.Models.Entities;

public class CancellationPolicy
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int FullRefundDeadlineDays { get; set; }
    public int PartialRefundDeadlineDays { get; set; }
    public int PartialRefundPercentage { get; set; }
    public int NoRefundAfterDays { get; set; }
    public decimal? CancellationFee { get; set; }

    // Navigation properties
    public Event Event { get; set; } = null!;
}
