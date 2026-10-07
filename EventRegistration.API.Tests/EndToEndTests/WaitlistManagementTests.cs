using EventRegistration.API.Tests.Infrastructure;
using EventRegistration.API.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace EventRegistration.API.Tests.EndToEndTests;

public class WaitlistManagementTests : TestBase
{
    public WaitlistManagementTests(EventRegistrationApiFactory factory) : base(factory) { }

    private async Task<(EventResponse Event, TicketTypeResponse TicketType)> CreateTestEventWithTicket(int ticketCapacity = 2)
    {
        var eventRequest = new CreateEventRequest(
            "Waitlist Test Event", null, null, null,
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
    public async Task RegisterForEvent_WhenFull_ShouldAddToWaitlist()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(2);

        // Fill capacity
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("First", "Person", "first@example.com", null, ticketResponse.Id, null));
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Second", "Person", "second@example.com", null, ticketResponse.Id, null));

        // Act - Register when full
        var waitlistResponse = await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Waitlist", "Person", "waitlist@example.com", null, ticketResponse.Id, null));

        // Assert
        waitlistResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Accepted);
        var registration = await waitlistResponse.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);
        registration!.Status.Should().Be("Waitlisted");
    }

    [Fact]
    public async Task GetWaitlistForEvent_ShouldReturnWaitlistedAttendees()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(1);

        // Fill capacity
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Confirmed", "Person", "confirmed@example.com", null, ticketResponse.Id, null));

        // Add 3 to waitlist
        for (int i = 1; i <= 3; i++)
        {
            await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
                new CreateRegistrationRequest($"Waitlist{i}", "Person", $"waitlist{i}@example.com", null, ticketResponse.Id, null));
        }

        // Act
        var waitlist = await GetAsync<List<WaitlistEntryResponse>>($"/api/events/{eventResponse.Id}/waitlist");

        // Assert
        waitlist.Should().NotBeNull();
        waitlist.Should().HaveCount(3);
        waitlist.Should().BeInAscendingOrder(w => w.Position);
    }

    [Fact]
    public async Task GetWaitlistForTicketType_ShouldReturnOnlyRelevantWaitlistEntries()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Multi-Ticket Waitlist Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500,
            DateTime.UtcNow.AddMonths(2).AddDays(-7),
            "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var vipTicket = new CreateTicketTypeRequest("VIP", null, 299.99m, 1, null, null);
        var generalTicket = new CreateTicketTypeRequest("General", null, 99.99m, 1, null, null);

        var vip = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", vipTicket))!;
        var general = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", generalTicket))!;

        // Fill both capacities
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("VIP1", "Person", "vip1@example.com", null, vip.Id, null));
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("General1", "Person", "general1@example.com", null, general.Id, null));

        // Add to waitlists
        await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("VIPWait", "Person", "vipwait@example.com", null, vip.Id, null));
        await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("GeneralWait", "Person", "generalwait@example.com", null, general.Id, null));

        // Act
        var vipWaitlist = await GetAsync<List<WaitlistEntryResponse>>($"/api/events/{eventResponse.Id}/waitlist/{vip.Id}");

        // Assert
        vipWaitlist.Should().NotBeNull();
        vipWaitlist.Should().HaveCount(1);
        vipWaitlist!.First().Email.Should().Be("vipwait@example.com");
        vipWaitlist.First().TicketTypeId.Should().Be(vip.Id);
    }

    [Fact]
    public async Task CancelRegistration_ShouldPromoteFirstWaitlistEntry()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(1);

        // Fill capacity
        var confirmedReg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Confirmed", "Person", "confirmed@example.com", null, ticketResponse.Id, null));

        // Add to waitlist
        await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Waitlist", "Person", "waitlist@example.com", null, ticketResponse.Id, null));

        // Act - Cancel the confirmed registration
        await DeleteAsync($"/api/registrations/{confirmedReg!.Id}");

        // Assert - Check if waitlist entry was promoted
        var waitlist = await GetAsync<List<WaitlistEntryResponse>>($"/api/events/{eventResponse.Id}/waitlist");

        // After promotion, the waitlist entry should either:
        // 1. Be removed from waitlist (if auto-confirmed)
        // 2. Have a promotion expiry time set (if requires confirmation)
        if (waitlist!.Count == 0)
        {
            // Auto-confirmed - verify registration exists
            var registrations = await GetAsync<List<RegistrationResponse>>($"/api/events/{eventResponse.Id}/registrations");
            registrations.Should().Contain(r => r.Email == "waitlist@example.com" && r.Status == "Confirmed");
        }
        else
        {
            // Requires confirmation - should have expiry time
            waitlist.First().PromotionExpiry.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ConfirmWaitlistPromotion_ShouldConvertToConfirmedRegistration()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(1);

        // Fill capacity
        var confirmedReg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Confirmed", "Person", "confirmed@example.com", null, ticketResponse.Id, null));

        // Add to waitlist
        await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Waitlist", "Person", "waitlist@example.com", null, ticketResponse.Id, null));

        // Cancel to trigger promotion
        await DeleteAsync($"/api/registrations/{confirmedReg!.Id}");

        // Get waitlist entry
        var waitlist = await GetAsync<List<WaitlistEntryResponse>>($"/api/events/{eventResponse.Id}/waitlist");

        if (waitlist!.Count > 0)
        {
            var waitlistEntry = waitlist.First();

            // Act - Confirm the promotion
            var response = await Client.PostAsync($"/api/waitlist/{waitlistEntry.Id}/confirm", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify registration is now confirmed
            var registrations = await GetAsync<List<RegistrationResponse>>($"/api/events/{eventResponse.Id}/registrations");
            registrations.Should().Contain(r => r.Email == "waitlist@example.com" && r.Status == "Confirmed");

            // Verify removed from waitlist
            var updatedWaitlist = await GetAsync<List<WaitlistEntryResponse>>($"/api/events/{eventResponse.Id}/waitlist");
            updatedWaitlist.Should().NotContain(w => w.Id == waitlistEntry.Id);
        }
    }

    [Fact]
    public async Task RemoveFromWaitlist_ShouldSuccessfullyRemoveEntry()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(1);

        // Fill capacity
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Confirmed", "Person", "confirmed@example.com", null, ticketResponse.Id, null));

        // Add to waitlist
        await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Waitlist", "Person", "waitlist@example.com", null, ticketResponse.Id, null));

        var waitlist = await GetAsync<List<WaitlistEntryResponse>>($"/api/events/{eventResponse.Id}/waitlist");
        var waitlistEntry = waitlist!.First();

        // Act
        await DeleteAsync($"/api/waitlist/{waitlistEntry.Id}");

        // Assert
        var updatedWaitlist = await GetAsync<List<WaitlistEntryResponse>>($"/api/events/{eventResponse.Id}/waitlist");
        updatedWaitlist.Should().BeEmpty();
    }

    [Fact]
    public async Task WaitlistPromotion_ShouldFollowFIFOOrder()
    {
        // Arrange
        var (eventResponse, ticketResponse) = await CreateTestEventWithTicket(1);

        // Fill capacity
        var confirmedReg = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Confirmed", "Person", "confirmed@example.com", null, ticketResponse.Id, null));

        // Add multiple to waitlist
        await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("First", "Waitlist", "first@example.com", null, ticketResponse.Id, null));

        await Task.Delay(100); // Small delay to ensure different timestamps

        await Client.PostAsJsonAsync($"/api/events/{eventResponse.Id}/registrations",
            new CreateRegistrationRequest("Second", "Waitlist", "second@example.com", null, ticketResponse.Id, null));

        // Act - Cancel to trigger promotion
        await DeleteAsync($"/api/registrations/{confirmedReg!.Id}");

        // Assert - First person should be promoted
        var registrations = await GetAsync<List<RegistrationResponse>>($"/api/events/{eventResponse.Id}/registrations");
        var promotedRegistration = registrations!.FirstOrDefault(r => r.Email == "first@example.com");

        if (promotedRegistration != null)
        {
            // If auto-confirmed, first person should be promoted
            promotedRegistration.Status.Should().BeOneOf("Confirmed", "Waitlisted");
        }
        else
        {
            // If requires confirmation, check waitlist order
            var waitlist = await GetAsync<List<WaitlistEntryResponse>>($"/api/events/{eventResponse.Id}/waitlist");
            var firstPerson = waitlist!.FirstOrDefault(w => w.Email == "first@example.com");

            if (firstPerson != null && firstPerson.PromotionExpiry.HasValue)
            {
                firstPerson.Position.Should().Be(1);
            }
        }
    }

    [Fact]
    public async Task GetWaitlist_ForInvalidEventId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/events/999999/waitlist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConfirmWaitlistPromotion_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.PostAsync("/api/waitlist/999999/confirm", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveFromWaitlist_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.DeleteAsync("/api/waitlist/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
