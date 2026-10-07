using EventRegistration.API.Tests.Infrastructure;
using EventRegistration.API.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace EventRegistration.API.Tests.EndToEndTests;

public class CapacityManagementTests : TestBase
{
    public CapacityManagementTests(EventRegistrationApiFactory factory) : base(factory) { }

    private async Task<(EventResponse Event, TicketTypeResponse TicketType)> CreateTestEventWithTicket(int ticketCapacity = 10)
    {
        var eventRequest = new CreateEventRequest(
            "Capacity Test Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500,
            DateTime.UtcNow.AddMonths(2).AddDays(-7),
            "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("General", null, 99.99m, ticketCapacity, null, null);
        var ticketResponse = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticketRequest))!;

        return (eventResponse, ticketResponse);
    }

    [Fact]
    public async Task GetCapacity_ForNewEvent_ShouldShowFullCapacityAvailable()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(100);

        // Act
        var capacity = await GetAsync<CapacityResponse>($"/api/events/{eventResponse.Id}/capacity");

        // Assert
        capacity.Should().NotBeNull();
        capacity!.EventId.Should().Be(eventResponse.Id);
        capacity.OverallCapacity.Should().Be(eventResponse.OverallCapacity);
        capacity.OverallRegistered.Should().Be(0);
        capacity.OverallAvailable.Should().Be(eventResponse.OverallCapacity);

        capacity.TicketTypeCapacities.Should().HaveCount(1);
        var ticketCapacity = capacity.TicketTypeCapacities.First();
        ticketCapacity.TicketTypeId.Should().Be(ticketResponse.Id);
        ticketCapacity.Capacity.Should().Be(100);
        ticketCapacity.Registered.Should().Be(0);
        ticketCapacity.Available.Should().Be(100);
    }

    [Fact]
    public async Task GetCapacity_AfterRegistrations_ShouldShowReducedAvailability()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(100);

        // Register 3 attendees
        for (int i = 0; i < 3; i++)
        {
            var registration = new CreateRegistrationRequest(
                $"Attendee{i}", "Test", $"attendee{i}@example.com", null, ticketResponse.Id, null
            );
            await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);
        }

        // Act
        var capacity = await GetAsync<CapacityResponse>($"/api/events/{eventResponse.Id}/capacity");

        // Assert
        capacity.Should().NotBeNull();
        capacity!.OverallRegistered.Should().Be(3);
        capacity.OverallAvailable.Should().Be(eventResponse.OverallCapacity - 3);

        var ticketCapacity = capacity.TicketTypeCapacities.First();
        ticketCapacity.Registered.Should().Be(3);
        ticketCapacity.Available.Should().Be(97);
    }

    [Fact]
    public async Task GetCapacity_WithMultipleTicketTypes_ShouldShowCapacityPerType()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Multi-Ticket Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            300,
            DateTime.UtcNow.AddMonths(2).AddDays(-7),
            "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var vipTicket = new CreateTicketTypeRequest("VIP", null, 299.99m, 50, null, null);
        var generalTicket = new CreateTicketTypeRequest("General", null, 99.99m, 250, null, null);

        var vip = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", vipTicket))!;
        var general = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", generalTicket))!;

        // Register 5 VIP and 10 General
        for (int i = 0; i < 5; i++)
        {
            await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
                new CreateRegistrationRequest($"VIP{i}", "Test", $"vip{i}@example.com", null, vip.Id, null));
        }
        for (int i = 0; i < 10; i++)
        {
            await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
                new CreateRegistrationRequest($"General{i}", "Test", $"general{i}@example.com", null, general.Id, null));
        }

        // Act
        var capacity = await GetAsync<CapacityResponse>($"/api/events/{eventResponse.Id}/capacity");

        // Assert
        capacity.Should().NotBeNull();
        capacity!.OverallRegistered.Should().Be(15);
        capacity.OverallAvailable.Should().Be(285);

        capacity.TicketTypeCapacities.Should().HaveCount(2);

        var vipCapacity = capacity.TicketTypeCapacities.First(t => t.TicketTypeId == vip.Id);
        vipCapacity.Registered.Should().Be(5);
        vipCapacity.Available.Should().Be(45);

        var generalCapacity = capacity.TicketTypeCapacities.First(t => t.TicketTypeId == general.Id);
        generalCapacity.Registered.Should().Be(10);
        generalCapacity.Available.Should().Be(240);
    }

    [Fact]
    public async Task RegisterForEvent_WhenTicketTypeCapacityFull_ShouldReturnBadRequestOrAddToWaitlist()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(2); // Only 2 spots

        // Fill the capacity
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("First", "Attendee", "first@example.com", null, ticketResponse.Id, null));
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Second", "Attendee", "second@example.com", null, ticketResponse.Id, null));

        // Act - Try to register when full
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Third", "Attendee", "third@example.com", null, ticketResponse.Id, null));

        // Assert - Should either fail or add to waitlist (depending on implementation)
        // If implementing automatic waitlist, check for 200 OK with waitlist status
        // If not, should return 400 Bad Request
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var registration = await response.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);
            registration!.Status.Should().Be("Waitlisted");
        }
    }

    [Fact]
    public async Task RegisterForEvent_WhenOverallEventCapacityFull_ShouldReturnBadRequestOrAddToWaitlist()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Small Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            5, // Only 5 overall capacity
            DateTime.UtcNow.AddMonths(2).AddDays(-7),
            "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticket = new CreateTicketTypeRequest("General", null, 99.99m, 5, null, null);
        var ticketResponse = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticket))!;

        // Fill the capacity
        for (int i = 0; i < 5; i++)
        {
            await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
                new CreateRegistrationRequest($"Attendee{i}", "Test", $"attendee{i}@example.com", null, ticketResponse.Id, null));
        }

        // Act - Try to register when event is full
        var response = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Extra", "Attendee", "extra@example.com", null, ticketResponse.Id, null));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var registration = await response.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);
            registration!.Status.Should().Be("Waitlisted");
        }
    }

    [Fact]
    public async Task GetCapacity_AfterCancellation_ShouldShowIncreasedAvailability()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(10);

        // Register 5 attendees
        var registrationIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var reg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
                new CreateRegistrationRequest($"Attendee{i}", "Test", $"attendee{i}@example.com", null, ticketResponse.Id, null));
            registrationIds.Add(reg!.Id);
        }

        // Cancel 2 registrations
        await DeleteAsync($"/api/registrations/{registrationIds[0]}");
        await DeleteAsync($"/api/registrations/{registrationIds[1]}");

        // Act
        var capacity = await GetAsync<CapacityResponse>($"/api/events/{eventResponse.Id}/capacity");

        // Assert
        capacity.Should().NotBeNull();
        capacity!.OverallRegistered.Should().Be(3); // 5 - 2 cancelled
        capacity.OverallAvailable.Should().Be(eventResponse.OverallCapacity - 3);

        var ticketCapacity = capacity.TicketTypeCapacities.First();
        ticketCapacity.Registered.Should().Be(3);
        ticketCapacity.Available.Should().Be(7);
    }

    [Fact]
    public async Task GetCapacity_ForInvalidEventId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/events/999999/capacity");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
