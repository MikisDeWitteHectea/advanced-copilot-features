using EventRegistration.API.Tests.Infrastructure;
using EventRegistration.API.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace EventRegistration.API.Tests.EndToEndTests;

public class TicketTypeManagementTests : TestBase
{
    public TicketTypeManagementTests(EventRegistrationApiFactory factory) : base(factory) { }

    private async Task<EventResponse> CreateTestEvent()
    {
        var request = new CreateEventRequest(
            "Test Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500,
            DateTime.UtcNow.AddMonths(2).AddDays(-7),
            "Published"
        );
        return (await PostAsync<EventResponse>("/api/events", request))!;
    }

    [Fact]
    public async Task AddTicketType_WithValidData_ShouldReturnCreatedTicketType()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var request = new CreateTicketTypeRequest(
            Name: "Early Bird",
            Description: "Early bird discount tickets",
            Price: 99.99m,
            Capacity: 100,
            AvailableFrom: DateTime.UtcNow,
            AvailableUntil: eventResponse.StartDate.AddDays(-30)
        );

        // Act
        var response = await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", request);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().BeGreaterThan(0);
        response.EventId.Should().Be(eventResponse.Id);
        response.Name.Should().Be(request.Name);
        response.Price.Should().Be(request.Price);
        response.Capacity.Should().Be(request.Capacity);
        response.AvailableCount.Should().Be(request.Capacity);
    }

    [Fact]
    public async Task AddTicketType_WithNegativePrice_ShouldReturnBadRequest()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var request = new CreateTicketTypeRequest(
            Name: "Invalid Ticket",
            Description: null,
            Price: -50.00m, // Negative price
            Capacity: 100,
            AvailableFrom: null,
            AvailableUntil: null
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/ticket-types", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTicketType_WithZeroOrNegativeCapacity_ShouldReturnBadRequest()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var request = new CreateTicketTypeRequest(
            Name: "Invalid Ticket",
            Description: null,
            Price: 50.00m,
            Capacity: 0, // Zero capacity
            AvailableFrom: null,
            AvailableUntil: null
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/ticket-types", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTicketType_WithCapacityExceedingEventCapacity_ShouldReturnBadRequest()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var request = new CreateTicketTypeRequest(
            Name: "Oversized Ticket",
            Description: null,
            Price: 100.00m,
            Capacity: eventResponse.OverallCapacity + 100, // Exceeds event capacity
            AvailableFrom: null,
            AvailableUntil: null
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/ticket-types", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTicketType_WithAvailableDatesOutsideEventPeriod_ShouldReturnBadRequest()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var request = new CreateTicketTypeRequest(
            Name: "Invalid Dates Ticket",
            Description: null,
            Price: 100.00m,
            Capacity: 50,
            AvailableFrom: DateTime.UtcNow,
            AvailableUntil: eventResponse.StartDate.AddDays(1) // After event start
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/ticket-types", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllTicketTypesForEvent_ShouldReturnListOfTicketTypes()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var earlyBird = new CreateTicketTypeRequest("Early Bird", null, 99.99m, 100, null, null);
        var vip = new CreateTicketTypeRequest("VIP", null, 299.99m, 50, null, null);
        var general = new CreateTicketTypeRequest("General Admission", null, 149.99m, 350, null, null);

        await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", earlyBird);
        await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", vip);
        await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", general);

        // Act
        var ticketTypes = await GetAsync<List<TicketTypeResponse>>($"/api/events/{eventResponse.Id}/ticket-types");

        // Assert
        ticketTypes.Should().NotBeNull();
        ticketTypes.Should().HaveCount(3);
        ticketTypes.Should().Contain(t => t.Name == "Early Bird");
        ticketTypes.Should().Contain(t => t.Name == "VIP");
        ticketTypes.Should().Contain(t => t.Name == "General Admission");
    }

    [Fact]
    public async Task GetTicketTypeById_WithValidId_ShouldReturnTicketType()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var createRequest = new CreateTicketTypeRequest("VIP Pass", "Premium access", 299.99m, 50, null, null);
        var createdTicket = await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", createRequest);

        // Act
        var retrievedTicket = await GetAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types/{createdTicket!.Id}");

        // Assert
        retrievedTicket.Should().NotBeNull();
        retrievedTicket!.Id.Should().Be(createdTicket.Id);
        retrievedTicket.Name.Should().Be(createRequest.Name);
        retrievedTicket.Description.Should().Be(createRequest.Description);
        retrievedTicket.Price.Should().Be(createRequest.Price);
    }

    [Fact]
    public async Task GetTicketTypeById_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/ticket-types/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTicketType_WithValidData_ShouldReturnUpdatedTicketType()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var createRequest = new CreateTicketTypeRequest("Original Ticket", "Original Description", 100.00m, 100, null, null);
        var createdTicket = await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", createRequest);

        var updateRequest = new UpdateTicketTypeRequest(
            Name: "Updated Ticket",
            Description: "Updated Description",
            Price: 120.00m,
            Capacity: 150,
            AvailableFrom: null,
            AvailableUntil: null
        );

        // Act
        var updatedTicket = await PutAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types/{createdTicket!.Id}", updateRequest);

        // Assert
        updatedTicket.Should().NotBeNull();
        updatedTicket!.Id.Should().Be(createdTicket.Id);
        updatedTicket.Name.Should().Be(updateRequest.Name);
        updatedTicket.Description.Should().Be(updateRequest.Description);
        updatedTicket.Price.Should().Be(updateRequest.Price);
        updatedTicket.Capacity.Should().Be(updateRequest.Capacity);
    }

    [Fact]
    public async Task UpdateTicketType_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var updateRequest = new UpdateTicketTypeRequest("Updated", null, 100.00m, 50, null, null);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/events/{eventResponse.Id}/ticket-types/999999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTicketType_WithNoRegistrations_ShouldSucceed()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var createRequest = new CreateTicketTypeRequest("Ticket To Delete", null, 50.00m, 50, null, null);
        var createdTicket = await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", createRequest);

        // Act
        await DeleteAsync($"/api/events/{eventResponse.Id}/ticket-types/{createdTicket!.Id}");

        // Assert - verify ticket type is deleted
        var getResponse = await Client.GetAsync($"/api/events/{eventResponse.Id}/ticket-types/{createdTicket.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTicketType_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();

        // Act
        var response = await Client.DeleteAsync($"/api/events/{eventResponse.Id}/ticket-types/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMultipleTicketTypes_WithTotalCapacityExceedingEventCapacity_ShouldReturnBadRequest()
    {
        // Arrange
        var eventResponse = await CreateTestEvent();
        var ticket1 = new CreateTicketTypeRequest("Ticket 1", null, 100.00m, 300, null, null);
        var ticket2 = new CreateTicketTypeRequest("Ticket 2", null, 100.00m, 250, null, null);

        await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticket1);

        // Act - Adding second ticket would exceed event capacity (300 + 250 > 500)
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/ticket-types", ticket2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
