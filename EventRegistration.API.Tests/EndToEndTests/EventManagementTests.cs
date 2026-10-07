using EventRegistration.API.Tests.Infrastructure;
using EventRegistration.API.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace EventRegistration.API.Tests.EndToEndTests;

public class EventManagementTests : TestBase
{
    public EventManagementTests(EventRegistrationApiFactory factory) : base(factory) { }

    [Fact]
    public async Task CreateEvent_WithValidData_ShouldReturnCreatedEvent()
    {
        // Arrange
        var request = new CreateEventRequest(
            Name: "Tech Conference 2025",
            Description: "Annual technology conference",
            VenueName: "Convention Center",
            VenueAddress: "123 Main St, City",
            StartDate: DateTime.UtcNow.AddMonths(2),
            EndDate: DateTime.UtcNow.AddMonths(2).AddDays(2),
            OverallCapacity: 500,
            RegistrationDeadline: DateTime.UtcNow.AddMonths(2).AddDays(-7),
            Status: "Draft"
        );

        // Act
        var response = await PostAsync<EventResponse>("/api/events", request);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().BeGreaterThan(0);
        response.Name.Should().Be(request.Name);
        response.Description.Should().Be(request.Description);
        response.VenueName.Should().Be(request.VenueName);
        response.OverallCapacity.Should().Be(request.OverallCapacity);
        response.Status.Should().Be(request.Status);
    }

    [Fact]
    public async Task CreateEvent_WithEndDateBeforeStartDate_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateEventRequest(
            Name: "Invalid Event",
            Description: null,
            VenueName: null,
            VenueAddress: null,
            StartDate: DateTime.UtcNow.AddMonths(2),
            EndDate: DateTime.UtcNow.AddMonths(1), // End before start
            OverallCapacity: 100,
            RegistrationDeadline: null,
            Status: "Draft"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_WithRegistrationDeadlineAfterStartDate_ShouldReturnBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddMonths(2);
        var request = new CreateEventRequest(
            Name: "Invalid Event",
            Description: null,
            VenueName: null,
            VenueAddress: null,
            StartDate: startDate,
            EndDate: startDate.AddDays(2),
            OverallCapacity: 100,
            RegistrationDeadline: startDate.AddDays(1), // After start date
            Status: "Draft"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_WithNegativeCapacity_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateEventRequest(
            Name: "Invalid Event",
            Description: null,
            VenueName: null,
            VenueAddress: null,
            StartDate: DateTime.UtcNow.AddMonths(2),
            EndDate: DateTime.UtcNow.AddMonths(2).AddDays(2),
            OverallCapacity: -10, // Negative capacity
            RegistrationDeadline: null,
            Status: "Draft"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllEvents_ShouldReturnListOfEvents()
    {
        // Arrange
        var event1 = new CreateEventRequest(
            "Event 1", null, null, null,
            DateTime.UtcNow.AddMonths(1),
            DateTime.UtcNow.AddMonths(1).AddDays(1),
            100, null, "Published"
        );
        var event2 = new CreateEventRequest(
            "Event 2", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(1),
            200, null, "Draft"
        );

        await PostAsync<EventResponse>("/api/events", event1);
        await PostAsync<EventResponse>("/api/events", event2);

        // Act
        var events = await GetAsync<List<EventResponse>>("/api/events");

        // Assert
        events.Should().NotBeNull();
        events.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllEvents_FilteredByStatus_ShouldReturnOnlyMatchingEvents()
    {
        // Arrange
        var publishedEvent = new CreateEventRequest(
            "Published Event", null, null, null,
            DateTime.UtcNow.AddMonths(1),
            DateTime.UtcNow.AddMonths(1).AddDays(1),
            100, null, "Published"
        );
        var draftEvent = new CreateEventRequest(
            "Draft Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(1),
            200, null, "Draft"
        );

        await PostAsync<EventResponse>("/api/events", publishedEvent);
        await PostAsync<EventResponse>("/api/events", draftEvent);

        // Act
        var events = await GetAsync<List<EventResponse>>("/api/events?status=Published");

        // Assert
        events.Should().NotBeNull();
        events.Should().OnlyContain(e => e.Status == "Published");
    }

    [Fact]
    public async Task GetEventById_WithValidId_ShouldReturnEvent()
    {
        // Arrange
        var createRequest = new CreateEventRequest(
            "Specific Event", "Test Description", null, null,
            DateTime.UtcNow.AddMonths(1),
            DateTime.UtcNow.AddMonths(1).AddDays(1),
            150, null, "Draft"
        );
        var createdEvent = await PostAsync<EventResponse>("/api/events", createRequest);

        // Act
        var retrievedEvent = await GetAsync<EventResponse>($"/api/events/{createdEvent!.Id}");

        // Assert
        retrievedEvent.Should().NotBeNull();
        retrievedEvent!.Id.Should().Be(createdEvent.Id);
        retrievedEvent.Name.Should().Be(createRequest.Name);
        retrievedEvent.Description.Should().Be(createRequest.Description);
    }

    [Fact]
    public async Task GetEventById_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/events/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEvent_WithValidData_ShouldReturnUpdatedEvent()
    {
        // Arrange
        var createRequest = new CreateEventRequest(
            "Original Event", "Original Description", null, null,
            DateTime.UtcNow.AddMonths(1),
            DateTime.UtcNow.AddMonths(1).AddDays(1),
            100, null, "Draft"
        );
        var createdEvent = await PostAsync<EventResponse>("/api/events", createRequest);

        var updateRequest = new UpdateEventRequest(
            Name: "Updated Event",
            Description: "Updated Description",
            VenueName: "New Venue",
            VenueAddress: "456 Updated St",
            StartDate: createdEvent!.StartDate,
            EndDate: createdEvent.EndDate,
            OverallCapacity: 150,
            RegistrationDeadline: null,
            Status: "Published"
        );

        // Act
        var updatedEvent = await PutAsync<EventResponse>($"/api/events/{createdEvent.Id}", updateRequest);

        // Assert
        updatedEvent.Should().NotBeNull();
        updatedEvent!.Id.Should().Be(createdEvent.Id);
        updatedEvent.Name.Should().Be(updateRequest.Name);
        updatedEvent.Description.Should().Be(updateRequest.Description);
        updatedEvent.VenueName.Should().Be(updateRequest.VenueName);
        updatedEvent.OverallCapacity.Should().Be(updateRequest.OverallCapacity);
        updatedEvent.Status.Should().Be(updateRequest.Status);
    }

    [Fact]
    public async Task UpdateEvent_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var updateRequest = new UpdateEventRequest(
            "Updated Event", null, null, null,
            DateTime.UtcNow.AddMonths(1),
            DateTime.UtcNow.AddMonths(1).AddDays(1),
            100, null, "Draft"
        );

        // Act
        var response = await Client.PutAsJsonAsync("/api/events/999999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEvent_WithNoRegistrations_ShouldSucceed()
    {
        // Arrange
        var createRequest = new CreateEventRequest(
            "Event To Delete", null, null, null,
            DateTime.UtcNow.AddMonths(1),
            DateTime.UtcNow.AddMonths(1).AddDays(1),
            100, null, "Draft"
        );
        var createdEvent = await PostAsync<EventResponse>("/api/events", createRequest);

        // Act
        await DeleteAsync($"/api/events/{createdEvent!.Id}");

        // Assert - verify event is deleted
        var getResponse = await Client.GetAsync($"/api/events/{createdEvent.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEvent_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.DeleteAsync("/api/events/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
