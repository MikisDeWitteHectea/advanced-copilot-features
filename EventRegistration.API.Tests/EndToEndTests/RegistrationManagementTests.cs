using EventRegistration.API.Tests.Infrastructure;
using EventRegistration.API.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace EventRegistration.API.Tests.EndToEndTests;

public class RegistrationManagementTests : TestBase
{
    public RegistrationManagementTests(EventRegistrationApiFactory factory) : base(factory) { }

    private async Task<(EventResponse Event, TicketTypeResponse TicketType)> CreateTestEventWithTicket()
    {
        var eventRequest = new CreateEventRequest(
            "Test Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500,
            DateTime.UtcNow.AddMonths(2).AddDays(-7),
            "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("General Admission", null, 99.99m, 100, null, null);
        var ticketResponse = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticketRequest))!;

        return (eventResponse, ticketResponse);
    }

    [Fact]
    public async Task RegisterForEvent_WithValidData_ShouldReturnConfirmedRegistration()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var request = new CreateRegistrationRequest(
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com",
            PhoneNumber: "+1234567890",
            TicketTypeId: ticketResponse.Id,
            DiscountCode: null
        );

        // Act
        var response = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", request);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().BeGreaterThan(0);
        response.EventId.Should().Be(eventResponse.Id);
        response.FirstName.Should().Be(request.FirstName);
        response.LastName.Should().Be(request.LastName);
        response.Email.Should().Be(request.Email);
        response.PhoneNumber.Should().Be(request.PhoneNumber);
        response.TicketTypeId.Should().Be(request.TicketTypeId);
        response.Status.Should().Be("Confirmed");
        response.TotalAmount.Should().Be(99.99m);
        response.RegistrationDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RegisterForEvent_WithMissingRequiredFields_ShouldReturnBadRequest()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var request = new CreateRegistrationRequest(
            FirstName: "", // Empty first name
            LastName: "Doe",
            Email: "john.doe@example.com",
            PhoneNumber: null,
            TicketTypeId: ticketResponse.Id,
            DiscountCode: null
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterForEvent_WithInvalidEmailFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var request = new CreateRegistrationRequest(
            FirstName: "John",
            LastName: "Doe",
            Email: "invalid-email", // Invalid email format
            PhoneNumber: null,
            TicketTypeId: ticketResponse.Id,
            DiscountCode: null
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterForEvent_ForDraftEvent_ShouldReturnBadRequest()
    {
        // Arrange
        var draftEventRequest = new CreateEventRequest(
            "Draft Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500, null, "Draft" // Draft status
        );
        var draftEvent = (await PostAsync<EventResponse>("/api/events", draftEventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("General", null, 99.99m, 100, null, null);
        var ticket = (await PostAsync<TicketTypeResponse>($"/api/events/{draftEvent.Id}/ticket-types", ticketRequest))!;

        var registrationRequest = new CreateRegistrationRequest(
            "John", "Doe", "john@example.com", null, ticket.Id, null
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{draftEvent.Id}/registrations", registrationRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterForEvent_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var email = "duplicate@example.com";

        var firstRegistration = new CreateRegistrationRequest(
            "John", "Doe", email, null, ticketResponse.Id, null
        );
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", firstRegistration);

        var secondRegistration = new CreateRegistrationRequest(
            "Jane", "Smith", email, null, ticketResponse.Id, null // Same email
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations", secondRegistration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterForEvent_WithInvalidTicketTypeId_ShouldReturnNotFound()
    {
        // Arrange
        var (eventResponse, _) = await CreateTestEventWithTicket();
        var request = new CreateRegistrationRequest(
            "John", "Doe", "john@example.com", null, 999999, null // Invalid ticket type ID
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllRegistrationsForEvent_ShouldReturnListOfRegistrations()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();

        var reg1 = new CreateRegistrationRequest("John", "Doe", "john@example.com", null, ticketResponse.Id, null);
        var reg2 = new CreateRegistrationRequest("Jane", "Smith", "jane@example.com", null, ticketResponse.Id, null);
        var reg3 = new CreateRegistrationRequest("Bob", "Johnson", "bob@example.com", null, ticketResponse.Id, null);

        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", reg1);
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", reg2);
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", reg3);

        // Act
        var registrations = await GetAsync<List<RegistrationResponse>>($"/api/events/{eventResponse.Id}/registrations");

        // Assert
        registrations.Should().NotBeNull();
        registrations.Should().HaveCount(3);
        registrations.Should().Contain(r => r.Email == "john@example.com");
        registrations.Should().Contain(r => r.Email == "jane@example.com");
        registrations.Should().Contain(r => r.Email == "bob@example.com");
    }

    [Fact]
    public async Task GetRegistrationById_WithValidId_ShouldReturnRegistration()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var request = new CreateRegistrationRequest("John", "Doe", "john@example.com", null, ticketResponse.Id, null);
        var createdRegistration = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", request);

        // Act
        var retrievedRegistration = await GetAsync<RegistrationResponse>($"/api/registrations/{createdRegistration!.Id}");

        // Assert
        retrievedRegistration.Should().NotBeNull();
        retrievedRegistration!.Id.Should().Be(createdRegistration.Id);
        retrievedRegistration.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task GetRegistrationById_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/registrations/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRegistrationsByEmail_ShouldReturnAllRegistrationsForEmail()
    {
        // Arrange
        var (event1, ticket1) = await CreateTestEventWithTicket();
        var (event2, ticket2) = await CreateTestEventWithTicket();

        var email = "multiregistration@example.com";

        var reg1 = new CreateRegistrationRequest("John", "Doe", email, null, ticket1.Id, null);
        var reg2 = new CreateRegistrationRequest("John", "Doe", email, null, ticket2.Id, null);

        await PostAsync<RegistrationResponse>($"/api/events/{event1.Id}/registrations", reg1);
        await PostAsync<RegistrationResponse>($"/api/events/{event2.Id}/registrations", reg2);

        // Act
        var registrations = await GetAsync<List<RegistrationResponse>>($"/api/registrations/by-email/{email}");

        // Assert
        registrations.Should().NotBeNull();
        registrations.Should().HaveCount(2);
        registrations.Should().AllSatisfy(r => r.Email.Should().Be(email));
    }

    [Fact]
    public async Task CancelRegistration_ShouldUpdateStatusToCancelled()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket();
        var request = new CreateRegistrationRequest("John", "Doe", "john@example.com", null, ticketResponse.Id, null);
        var registration = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", request);

        // Act
        await DeleteAsync($"/api/registrations/{registration!.Id}");

        // Assert - verify registration status changed
        var updatedRegistration = await GetAsync<RegistrationResponse>($"/api/registrations/{registration.Id}");
        updatedRegistration!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelRegistration_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.DeleteAsync("/api/registrations/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
