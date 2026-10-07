namespace EventRegistration.API.Tests.Models;

// Event DTOs
public record CreateEventRequest(
    string Name,
    string? Description,
    string? VenueName,
    string? VenueAddress,
    DateTime StartDate,
    DateTime EndDate,
    int OverallCapacity,
    DateTime? RegistrationDeadline,
    string Status
);

public record UpdateEventRequest(
    string Name,
    string? Description,
    string? VenueName,
    string? VenueAddress,
    DateTime StartDate,
    DateTime EndDate,
    int OverallCapacity,
    DateTime? RegistrationDeadline,
    string Status
);

public record EventResponse(
    int Id,
    string Name,
    string? Description,
    string? VenueName,
    string? VenueAddress,
    DateTime StartDate,
    DateTime EndDate,
    int OverallCapacity,
    DateTime? RegistrationDeadline,
    string Status
);

// Ticket Type DTOs
public record CreateTicketTypeRequest(
    string Name,
    string? Description,
    decimal Price,
    int Capacity,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil
);

public record UpdateTicketTypeRequest(
    string Name,
    string? Description,
    decimal Price,
    int Capacity,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil
);

public record TicketTypeResponse(
    int Id,
    int EventId,
    string Name,
    string? Description,
    decimal Price,
    int Capacity,
    int AvailableCount,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil
);

// Registration DTOs
public record CreateRegistrationRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    int TicketTypeId,
    string? DiscountCode
);

public record RegistrationResponse(
    int Id,
    int EventId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    int TicketTypeId,
    DateTime RegistrationDate,
    string Status,
    decimal TotalAmount,
    string? DiscountCodeUsed
);

// Capacity DTOs
public record CapacityResponse(
    int EventId,
    int OverallCapacity,
    int OverallRegistered,
    int OverallAvailable,
    List<TicketTypeCapacity> TicketTypeCapacities
);

public record TicketTypeCapacity(
    int TicketTypeId,
    string TicketTypeName,
    int Capacity,
    int Registered,
    int Available
);

// Waitlist DTOs
public record WaitlistEntryResponse(
    int Id,
    int EventId,
    int TicketTypeId,
    string FirstName,
    string LastName,
    string Email,
    int Position,
    DateTime JoinedDate,
    DateTime? PromotionExpiry
);

// Discount Code DTOs
public record CreateDiscountCodeRequest(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    DateTime ValidFrom,
    DateTime ValidUntil,
    List<int>? ApplicableTicketTypeIds
);

public record UpdateDiscountCodeRequest(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    DateTime ValidFrom,
    DateTime ValidUntil,
    List<int>? ApplicableTicketTypeIds,
    string Status
);

public record DiscountCodeResponse(
    int Id,
    int EventId,
    string Code,
    string DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    int CurrentUses,
    DateTime ValidFrom,
    DateTime ValidUntil,
    List<int>? ApplicableTicketTypeIds,
    string Status
);

public record ValidateDiscountCodeRequest(
    string Code,
    int TicketTypeId
);

public record ValidateDiscountCodeResponse(
    bool IsValid,
    string? ErrorMessage,
    decimal? DiscountAmount
);

// Cancellation Policy DTOs
public record CreateCancellationPolicyRequest(
    int FullRefundDeadlineDays,
    int PartialRefundDeadlineDays,
    int PartialRefundPercentage,
    int NoRefundAfterDays,
    decimal? CancellationFee
);

public record CancellationPolicyResponse(
    int Id,
    int EventId,
    int FullRefundDeadlineDays,
    int PartialRefundDeadlineDays,
    int PartialRefundPercentage,
    int NoRefundAfterDays,
    decimal? CancellationFee
);

public record CancelRegistrationResponse(
    int RegistrationId,
    string Status,
    decimal RefundAmount,
    string RefundReason
);

// Export DTOs
public record AttendeeExportRecord(
    int RegistrationId,
    string AttendeeName,
    string Email,
    string? PhoneNumber,
    string TicketType,
    DateTime RegistrationDate,
    decimal AmountPaid,
    string Status,
    string? DiscountCodeUsed
);
