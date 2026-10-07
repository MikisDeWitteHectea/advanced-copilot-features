namespace EventRegistration.UI.Models;

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

public record CreateTicketTypeRequest(
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

public record CreateDiscountCodeRequest(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    List<int>? ApplicableTicketTypeIds
);

public record DiscountCodeResponse(
    int Id,
    int EventId,
    string Code,
    string DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    int CurrentUses,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    List<int>? ApplicableTicketTypeIds,
    string Status
);

public record CreateCancellationPolicyRequest(
    int FullRefundDeadlineDays,
    int PartialRefundDeadlineDays,
    decimal PartialRefundPercentage,
    int NoRefundAfterDays,
    decimal? CancellationFee
);

public record CancellationPolicyResponse(
    int Id,
    int EventId,
    int FullRefundDeadlineDays,
    int PartialRefundDeadlineDays,
    decimal PartialRefundPercentage,
    int NoRefundAfterDays,
    decimal? CancellationFee
);

public record CapacityInfo(
    int OverallCapacity,
    int TotalRegistered,
    int OverallAvailable,
    decimal OverallUtilizationPercentage,
    bool IsOverallFull,
    List<TicketTypeCapacityInfo> TicketTypeCapacities
);

public record TicketTypeCapacityInfo(
    string TicketTypeName,
    int Capacity,
    int Registered,
    int Available,
    decimal UtilizationPercentage,
    bool IsFull
);
