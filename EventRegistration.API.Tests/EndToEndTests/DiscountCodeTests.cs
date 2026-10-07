using EventRegistration.API.Tests.Infrastructure;
using EventRegistration.API.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace EventRegistration.API.Tests.EndToEndTests;

public class DiscountCodeTests : TestBase
{
    public DiscountCodeTests(EventRegistrationApiFactory factory) : base(factory) { }

    private async Task<(EventResponse Event, TicketTypeResponse TicketType)> CreateTestEventWithTicket()
    {
        var eventRequest = new CreateEventRequest(
            "Discount Test Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500,
            DateTime.UtcNow.AddMonths(2).AddDays(-7),
            "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("General", null, 100.00m, 100, null, null);
        var ticketResponse = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticketRequest))!;

        return (eventResponse, ticketResponse);
    }

    [Fact]
    public async Task CreateDiscountCode_WithPercentageDiscount_ShouldReturnCreatedCode()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var request = new CreateDiscountCodeRequest(
            Code: "EARLYBIRD20",
            DiscountType: "Percentage",
            DiscountValue: 20m,
            MaxUses: 50,
            ValidFrom: DateTime.UtcNow,
            ValidUntil: eventResponse.StartDate.AddDays(-1),
            ApplicableTicketTypeIds: null
        );

        // Act
        var response = await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", request);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().BeGreaterThan(0);
        response.EventId.Should().Be(eventResponse.Id);
        response.Code.Should().Be(request.Code);
        response.DiscountType.Should().Be(request.DiscountType);
        response.DiscountValue.Should().Be(request.DiscountValue);
        response.MaxUses.Should().Be(request.MaxUses);
        response.CurrentUses.Should().Be(0);
        response.Status.Should().Be("Active");
    }

    [Fact]
    public async Task CreateDiscountCode_WithFixedAmountDiscount_ShouldReturnCreatedCode()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var request = new CreateDiscountCodeRequest(
            Code: "SAVE25",
            DiscountType: "FixedAmount",
            DiscountValue: 25.00m,
            MaxUses: null, // Unlimited
            ValidFrom: DateTime.UtcNow,
            ValidUntil: eventResponse.StartDate,
            ApplicableTicketTypeIds: null
        );

        // Act
        var response = await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", request);

        // Assert
        response.Should().NotBeNull();
        response!.Code.Should().Be(request.Code);
        response.DiscountType.Should().Be(request.DiscountType);
        response.DiscountValue.Should().Be(request.DiscountValue);
        response.MaxUses.Should().BeNull();
    }

    [Fact]
    public async Task CreateDiscountCode_WithInvalidPercentage_ShouldReturnBadRequest()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var request = new CreateDiscountCodeRequest(
            Code: "INVALID",
            DiscountType: "Percentage",
            DiscountValue: 150m, // Invalid percentage > 100
            MaxUses: null,
            ValidFrom: DateTime.UtcNow,
            ValidUntil: eventResponse.StartDate,
            ApplicableTicketTypeIds: null
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/discount-codes", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDiscountCode_WithDuplicateCode_ShouldReturnBadRequest()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var code = "DUPLICATE";

        var request1 = new CreateDiscountCodeRequest(
            code, "Percentage", 10m, null,
            DateTime.UtcNow, eventResponse.StartDate, null
        );
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", request1);

        var request2 = new CreateDiscountCodeRequest(
            code, "Percentage", 15m, null,
            DateTime.UtcNow, eventResponse.StartDate, null
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/discount-codes", request2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllDiscountCodesForEvent_ShouldReturnListOfCodes()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();

        var code1 = new CreateDiscountCodeRequest("CODE1", "Percentage", 10m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        var code2 = new CreateDiscountCodeRequest("CODE2", "FixedAmount", 20m, null, DateTime.UtcNow, eventResponse.StartDate, null);

        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", code1);
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", code2);

        // Act
        var codes = await GetAsync<List<DiscountCodeResponse>>($"/api/events/{eventResponse.Id}/discount-codes");

        // Assert
        codes.Should().NotBeNull();
        codes.Should().HaveCount(2);
        codes.Should().Contain(c => c.Code == "CODE1");
        codes.Should().Contain(c => c.Code == "CODE2");
    }

    [Fact]
    public async Task GetDiscountCodeByCode_WithValidCode_ShouldReturnCode()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var request = new CreateDiscountCodeRequest("TESTCODE", "Percentage", 15m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", request);

        // Act
        var code = await GetAsync<DiscountCodeResponse>("/api/discount-codes/TESTCODE");

        // Assert
        code.Should().NotBeNull();
        code!.Code.Should().Be("TESTCODE");
    }

    [Fact]
    public async Task GetDiscountCodeByCode_WithInvalidCode_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/discount-codes/NONEXISTENT");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateDiscountCode_WithValidData_ShouldReturnUpdatedCode()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var createRequest = new CreateDiscountCodeRequest("UPDATE", "Percentage", 10m, 100, DateTime.UtcNow, eventResponse.StartDate, null);
        var created = await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", createRequest);

        var updateRequest = new UpdateDiscountCodeRequest(
            Code: "UPDATED",
            DiscountType: "Percentage",
            DiscountValue: 15m,
            MaxUses: 150,
            ValidFrom: DateTime.UtcNow,
            ValidUntil: eventResponse.StartDate,
            ApplicableTicketTypeIds: null,
            Status: "Active"
        );

        // Act
        var updated = await PutAsync<DiscountCodeResponse>($"/api/discount-codes/{created!.Id}", updateRequest);

        // Assert
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(created.Id);
        updated.Code.Should().Be(updateRequest.Code);
        updated.DiscountValue.Should().Be(updateRequest.DiscountValue);
        updated.MaxUses.Should().Be(updateRequest.MaxUses);
    }

    [Fact]
    public async Task UpdateDiscountCode_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var updateRequest = new UpdateDiscountCodeRequest(
            "TEST", "Percentage", 10m, null,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null, "Active"
        );

        // Act
        var response = await Client.PutAsJsonAsync("/api/discount-codes/999999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteDiscountCode_WithNoUsages_ShouldSucceed()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var request = new CreateDiscountCodeRequest("DELETE", "Percentage", 10m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        var created = await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", request);

        // Act
        await DeleteAsync($"/api/discount-codes/{created!.Id}");

        // Assert
        var getResponse = await Client.GetAsync($"/api/discount-codes/{created.Code}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidateDiscountCode_WithValidCode_ShouldReturnValid()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var discountRequest = new CreateDiscountCodeRequest("VALID20", "Percentage", 20m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        var validateRequest = new ValidateDiscountCodeRequest("VALID20", ticketResponse.Id);

        // Act
        var validation = await PostAsync<ValidateDiscountCodeResponse>("/api/discount-codes/VALID20/validate", validateRequest);

        // Assert
        validation.Should().NotBeNull();
        validation!.IsValid.Should().BeTrue();
        validation.ErrorMessage.Should().BeNullOrEmpty();
        validation.DiscountAmount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateDiscountCode_WithExpiredCode_ShouldReturnInvalid()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var discountRequest = new CreateDiscountCodeRequest(
            "EXPIRED", "Percentage", 20m, null,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(-1), // Already expired
            null
        );
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        var validateRequest = new ValidateDiscountCodeRequest("EXPIRED", ticketResponse.Id);

        // Act
        var validation = await PostAsync<ValidateDiscountCodeResponse>("/api/discount-codes/EXPIRED/validate", validateRequest);

        // Assert
        validation.Should().NotBeNull();
        validation!.IsValid.Should().BeFalse();
        validation.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateDiscountCode_WithMaxUsesReached_ShouldReturnInvalid()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var discountRequest = new CreateDiscountCodeRequest("LIMITED", "Percentage", 10m, 1, DateTime.UtcNow, eventResponse.StartDate, null);
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        // Use the code once
        var registration = new CreateRegistrationRequest("User", "One", "user1@example.com", null, ticketResponse.Id, "LIMITED");
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        var validateRequest = new ValidateDiscountCodeRequest("LIMITED", ticketResponse.Id);

        // Act
        var validation = await PostAsync<ValidateDiscountCodeResponse>("/api/discount-codes/LIMITED/validate", validateRequest);

        // Assert
        validation.Should().NotBeNull();
        validation!.IsValid.Should().BeFalse();
        validation.ErrorMessage.Should().Contain("maximum uses");
    }

    [Fact]
    public async Task RegisterWithDiscountCode_WithPercentageDiscount_ShouldApplyCorrectDiscount()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var discountRequest = new CreateDiscountCodeRequest("PERCENT25", "Percentage", 25m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        var registration = new CreateRegistrationRequest("John", "Doe", "john@example.com", null, ticketResponse.Id, "PERCENT25");

        // Act
        var response = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Assert
        response.Should().NotBeNull();
        response!.DiscountCodeUsed.Should().Be("PERCENT25");
        response.TotalAmount.Should().Be(75.00m); // 100 - 25% = 75
    }

    [Fact]
    public async Task RegisterWithDiscountCode_WithFixedAmountDiscount_ShouldApplyCorrectDiscount()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var discountRequest = new CreateDiscountCodeRequest("SAVE30", "FixedAmount", 30m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        var registration = new CreateRegistrationRequest("Jane", "Smith", "jane@example.com", null, ticketResponse.Id, "SAVE30");

        // Act
        var response = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Assert
        response.Should().NotBeNull();
        response!.DiscountCodeUsed.Should().Be("SAVE30");
        response.TotalAmount.Should().Be(70.00m); // 100 - 30 = 70
    }

    [Fact]
    public async Task RegisterWithDiscountCode_WithInvalidCode_ShouldReturnBadRequest()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var registration = new CreateRegistrationRequest("John", "Doe", "john@example.com", null, ticketResponse.Id, "INVALIDCODE");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterWithDiscountCode_ShouldIncrementUseCount()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var discountRequest = new CreateDiscountCodeRequest("COUNTER", "Percentage", 10m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        var discountCode = await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        // Act - Use the code
        var registration = new CreateRegistrationRequest("User", "Test", "user@example.com", null, ticketResponse.Id, "COUNTER");
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Assert - Check use count
        var updatedCode = await GetAsync<DiscountCodeResponse>($"/api/discount-codes/{discountCode!.Code}");
        updatedCode!.CurrentUses.Should().Be(1);
    }

    [Fact]
    public async Task CreateDiscountCode_WithSpecificTicketTypes_ShouldOnlyApplyToThoseTypes()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Multi-Ticket Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500, null, "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var vipTicket = new CreateTicketTypeRequest("VIP", null, 200m, 50, null, null);
        var generalTicket = new CreateTicketTypeRequest("General", null, 100m, 450, null, null);

        var vip = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", vipTicket))!;
        var general = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", generalTicket))!;

        // Create discount only for VIP tickets
        var discountRequest = new CreateDiscountCodeRequest(
            "VIPONLY", "Percentage", 20m, null,
            DateTime.UtcNow, eventResponse.StartDate,
            new List<int> { vip.Id }
        );
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        // Act - Try to use code on general ticket
        var generalReg = new CreateRegistrationRequest("General", "User", "general@example.com", null, general.Id, "VIPONLY");
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations", generalReg);

        // Assert - Should fail as code is only for VIP
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
