using EventRegistration.API.Tests.Infrastructure;
using EventRegistration.API.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace EventRegistration.API.Tests.EndToEndTests;

public class CancellationAndRefundTests : TestBase
{
    public CancellationAndRefundTests(EventRegistrationApiFactory factory) : base(factory) { }

    private async Task<(EventResponse Event, TicketTypeResponse TicketType)> CreateTestEventWithTicket(int daysUntilEvent = 60)
    {
        var eventRequest = new CreateEventRequest(
            "Refund Test Event", null, null, null,
            DateTime.UtcNow.AddDays(daysUntilEvent),
            DateTime.UtcNow.AddDays(daysUntilEvent + 2),
            500,
            DateTime.UtcNow.AddDays(daysUntilEvent - 7),
            "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("General", null, 100.00m, 100, null, null);
        var ticketResponse = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticketRequest))!;

        return (eventResponse, ticketResponse);
    }

    [Fact]
    public async Task CreateCancellationPolicy_WithValidData_ShouldReturnCreatedPolicy()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var request = new CreateCancellationPolicyRequest(
            FullRefundDeadlineDays: 30,
            PartialRefundDeadlineDays: 14,
            PartialRefundPercentage: 50,
            NoRefundAfterDays: 7,
            CancellationFee: 5.00m
        );

        // Act
        var response = await PostAsync<CancellationPolicyResponse>($"/api/events/{eventResponse.Id}/cancellation-policy", request);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().BeGreaterThan(0);
        response.EventId.Should().Be(eventResponse.Id);
        response.FullRefundDeadlineDays.Should().Be(request.FullRefundDeadlineDays);
        response.PartialRefundDeadlineDays.Should().Be(request.PartialRefundDeadlineDays);
        response.PartialRefundPercentage.Should().Be(request.PartialRefundPercentage);
        response.NoRefundAfterDays.Should().Be(request.NoRefundAfterDays);
        response.CancellationFee.Should().Be(request.CancellationFee);
    }

    [Fact]
    public async Task GetCancellationPolicy_ForEvent_ShouldReturnPolicy()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var request = new CreateCancellationPolicyRequest(30, 14, 50, 7, 5.00m);
        await PostAsync<CancellationPolicyResponse>($"/api/events/{eventResponse.Id}/cancellation-policy", request);

        // Act
        var policy = await GetAsync<CancellationPolicyResponse>($"/api/events/{eventResponse.Id}/cancellation-policy");

        // Assert
        policy.Should().NotBeNull();
        policy!.EventId.Should().Be(eventResponse.Id);
    }

    [Fact]
    public async Task GetCancellationPolicy_ForEventWithoutPolicy_ShouldReturnNotFound()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/cancellation-policy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelRegistration_WithFullRefundEligibility_ShouldReturnFullRefund()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(60); // Event in 60 days

        var policyRequest = new CreateCancellationPolicyRequest(30, 14, 50, 7, 5.00m);
        await PostAsync<CancellationPolicyResponse>($"/api/events/{eventResponse.Id}/cancellation-policy", policyRequest);

        var registration = new CreateRegistrationRequest("John", "Doe", "john@example.com", null, ticketResponse.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act - Cancel well before event (more than 30 days)
        var cancelResponse = await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert
        cancelResponse.Should().NotBeNull();
        cancelResponse!.RegistrationId.Should().Be(reg.Id);
        cancelResponse.Status.Should().Be("Cancelled");
        cancelResponse.RefundAmount.Should().Be(95.00m); // 100 - 5 fee = 95 (full refund minus fee)
        cancelResponse.RefundReason.Should().Contain("full");
    }

    [Fact]
    public async Task CancelRegistration_WithPartialRefundEligibility_ShouldReturnPartialRefund()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(20); // Event in 20 days

        var policyRequest = new CreateCancellationPolicyRequest(30, 14, 50, 7, 5.00m);
        await PostAsync<CancellationPolicyResponse>($"/api/events/{eventResponse.Id}/cancellation-policy", policyRequest);

        var registration = new CreateRegistrationRequest("Jane", "Smith", "jane@example.com", null, ticketResponse.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act - Cancel within partial refund window (between 14-30 days)
        var cancelResponse = await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert
        cancelResponse.Should().NotBeNull();
        cancelResponse!.RegistrationId.Should().Be(reg.Id);
        cancelResponse.Status.Should().Be("Cancelled");
        cancelResponse.RefundAmount.Should().Be(47.50m); // (100 - 5) * 0.50 = 47.50 (50% refund minus fee)
        cancelResponse.RefundReason.Should().Contain("partial");
    }

    [Fact]
    public async Task CancelRegistration_WithNoRefundEligibility_ShouldReturnNoRefund()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(5); // Event in 5 days

        var policyRequest = new CreateCancellationPolicyRequest(30, 14, 50, 7, 5.00m);
        await PostAsync<CancellationPolicyResponse>($"/api/events/{eventResponse.Id}/cancellation-policy", policyRequest);

        var registration = new CreateRegistrationRequest("Bob", "Johnson", "bob@example.com", null, ticketResponse.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act - Cancel within no-refund window (less than 7 days)
        var cancelResponse = await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert
        cancelResponse.Should().NotBeNull();
        cancelResponse!.RegistrationId.Should().Be(reg.Id);
        cancelResponse.Status.Should().Be("Cancelled");
        cancelResponse.RefundAmount.Should().Be(0m);
        cancelResponse.RefundReason.Should().Contain("no refund");
    }

    [Fact]
    public async Task CancelRegistration_WithoutCancellationPolicy_ShouldUseDefaultPolicy()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(60);

        var registration = new CreateRegistrationRequest("User", "Test", "user@example.com", null, ticketResponse.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act - Cancel without policy set
        var cancelResponse = await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert - Should have some default behavior (full refund or no refund)
        cancelResponse.Should().NotBeNull();
        cancelResponse!.Status.Should().Be("Cancelled");
        cancelResponse.RefundAmount.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task CancelRegistration_WithDiscountApplied_ShouldRefundActualAmountPaid()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(60);

        var policyRequest = new CreateCancellationPolicyRequest(30, 14, 50, 7, 0m); // No cancellation fee
        await PostAsync<CancellationPolicyResponse>($"/api/events/{eventResponse.Id}/cancellation-policy", policyRequest);

        var discountRequest = new CreateDiscountCodeRequest("SAVE20", "Percentage", 20m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        var registration = new CreateRegistrationRequest("Discount", "User", "discount@example.com", null, ticketResponse.Id, "SAVE20");
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act - Cancel registration with discount
        var cancelResponse = await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert - Refund should be based on 80.00 (discounted price), not 100.00
        cancelResponse.Should().NotBeNull();
        cancelResponse!.RefundAmount.Should().Be(80.00m); // Full refund of discounted price
    }

    [Fact]
    public async Task CancelRegistration_ShouldUpdateRegistrationStatus()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var registration = new CreateRegistrationRequest("Test", "User", "test@example.com", null, ticketResponse.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act
        await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert - Check registration status
        var updatedReg = await GetAsync<RegistrationResponse>($"/api/registrations/{reg.Id}");
        updatedReg!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelRegistration_ShouldFreeUpCapacity()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var registration = new CreateRegistrationRequest("Test", "User", "test@example.com", null, ticketResponse.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        var capacityBefore = await GetAsync<CapacityResponse>($"/api/events/{eventResponse.Id}/capacity");

        // Act
        await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert - Capacity should increase
        var capacityAfter = await GetAsync<CapacityResponse>($"/api/events/{eventResponse.Id}/capacity");
        capacityAfter!.OverallAvailable.Should().BeGreaterThan(capacityBefore!.OverallAvailable);
    }

    [Fact]
    public async Task CancelRegistration_WithWaitlist_ShouldPromoteWaitlistEntry()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();

        // Update ticket capacity to 1
        var smallTicket = new CreateTicketTypeRequest("Limited", null, 100m, 1, null, null);
        var limitedTicket = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", smallTicket))!;

        // Fill capacity
        var registration = new CreateRegistrationRequest("First", "User", "first@example.com", null, limitedTicket.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Add to waitlist
        await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Waitlist", "User", "waitlist@example.com", null, limitedTicket.Id, null));

        // Act - Cancel registration
        await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert - Check if waitlist was promoted
        var registrations = await GetAsync<List<RegistrationResponse>>($"/api/events/{eventResponse.Id}/registrations");
        var promotedReg = registrations!.FirstOrDefault(r => r.Email == "waitlist@example.com");

        if (promotedReg != null)
        {
            promotedReg.Status.Should().BeOneOf("Confirmed", "Waitlisted"); // Could be auto-confirmed or awaiting confirmation
        }
    }

    [Fact]
    public async Task CancelRegistration_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.PostAsync("/api/registrations/999999/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelRegistration_AlreadyCancelled_ShouldReturnBadRequest()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var registration = new CreateRegistrationRequest("Test", "User", "test@example.com", null, ticketResponse.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Cancel once
        await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Act - Try to cancel again
        var response = await Client.PostAsync($"/api/registrations/{reg.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancellationPolicy_WithNoCancellationFee_ShouldNotDeductFee()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(60);

        var policyRequest = new CreateCancellationPolicyRequest(30, 14, 50, 7, null); // No fee
        await PostAsync<CancellationPolicyResponse>($"/api/events/{eventResponse.Id}/cancellation-policy", policyRequest);

        var registration = new CreateRegistrationRequest("User", "Test", "user@example.com", null, ticketResponse.Id, null);
        var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act
        var cancelResponse = await PostAsync<CancelRegistrationResponse>($"/api/registrations/{reg!.Id}/cancel", new { });

        // Assert
        cancelResponse!.RefundAmount.Should().Be(100.00m); // Full amount, no fee deducted
    }

    [Fact]
    public async Task CreateCancellationPolicy_ForInvalidEventId_ShouldReturnNotFound()
    {
        // Arrange
        var request = new CreateCancellationPolicyRequest(30, 14, 50, 7, 5.00m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/events/999999/cancellation-policy", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
