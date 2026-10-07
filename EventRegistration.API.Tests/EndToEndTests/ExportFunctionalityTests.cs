using EventRegistration.API.Tests.Infrastructure;
using EventRegistration.API.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace EventRegistration.API.Tests.EndToEndTests;

public class ExportFunctionalityTests : TestBase
{
    public ExportFunctionalityTests(EventRegistrationApiFactory factory) : base(factory) { }

    private async Task<(EventResponse Event, TicketTypeResponse TicketType, List<RegistrationResponse> Registrations)> CreateTestEventWithRegistrations()
    {
        var eventRequest = new CreateEventRequest(
            "Export Test Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500,
            DateTime.UtcNow.AddMonths(2).AddDays(-7),
            "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("General", null, 99.99m, 100, null, null);
        var ticketResponse = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticketRequest))!;

        var registrations = new List<RegistrationResponse>();
        for (int i = 1; i <= 5; i++)
        {
            var reg = new CreateRegistrationRequest($"Attendee{i}", "Test", $"attendee{i}@example.com", $"+123456789{i}", ticketResponse.Id, null);
            var regResponse = await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", reg);
            registrations.Add(regResponse!);
        }

        return (eventResponse, ticketResponse, registrations);
    }

    [Fact]
    public async Task ExportAttendeesAsJson_ShouldReturnValidJsonArray()
    {
        // Arrange
        var (eventResponse, _, _) = await CreateTestEventWithRegistrations();

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var jsonContent = await response.Content.ReadAsStringAsync();
        var attendees = JsonSerializer.Deserialize<List<AttendeeExportRecord>>(jsonContent, JsonOptions);

        attendees.Should().NotBeNull();
        attendees.Should().HaveCount(5);
        attendees!.Should().AllSatisfy(a =>
        {
            a.RegistrationId.Should().BeGreaterThan(0);
            a.AttendeeName.Should().NotBeNullOrEmpty();
            a.Email.Should().NotBeNullOrEmpty();
            a.TicketType.Should().NotBeNullOrEmpty();
            a.Status.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task ExportAttendeesAsCsv_ShouldReturnValidCsvFormat()
    {
        // Arrange
        var (eventResponse, _, _) = await CreateTestEventWithRegistrations();

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/csv");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");

        var csvContent = await response.Content.ReadAsStringAsync();
        csvContent.Should().NotBeNullOrEmpty();

        // Verify CSV structure
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().BeGreaterOrEqualTo(6); // Header + 5 records

        // Check header
        var header = lines[0];
        header.Should().Contain("RegistrationId");
        header.Should().Contain("AttendeeName");
        header.Should().Contain("Email");
        header.Should().Contain("TicketType");
        header.Should().Contain("Status");

        // Check data rows
        for (int i = 1; i < lines.Length; i++)
        {
            lines[i].Should().Contain("@example.com");
        }
    }

    [Fact]
    public async Task ExportAttendeesAsExcel_ShouldReturnExcelFile()
    {
        // Arrange
        var (eventResponse, _, _) = await CreateTestEventWithRegistrations();

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/excel");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        contentType.Should().BeOneOf(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-excel"
        );

        var content = await response.Content.ReadAsByteArrayAsync();
        content.Should().NotBeEmpty();
        content.Length.Should().BeGreaterThan(100); // Excel files have a minimum size
    }

    [Fact]
    public async Task ExportAttendees_WithStatusFilter_ShouldReturnOnlyMatchingRecords()
    {
        // Arrange
        var (eventResponse, ticketResponse, registrations) = await CreateTestEventWithRegistrations();

        // Cancel one registration
        await DeleteAsync($"/api/registrations/{registrations[0].Id}");

        // Act - Export only confirmed registrations
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json?status=Confirmed");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var attendees = JsonSerializer.Deserialize<List<AttendeeExportRecord>>(jsonContent, JsonOptions);

        attendees.Should().NotBeNull();
        attendees.Should().HaveCount(4); // 5 - 1 cancelled
        attendees.Should().AllSatisfy(a => a.Status.Should().Be("Confirmed"));
    }

    [Fact]
    public async Task ExportAttendees_WithTicketTypeFilter_ShouldReturnOnlyMatchingRecords()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Multi-Ticket Export Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500, null, "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var vipTicket = new CreateTicketTypeRequest("VIP", null, 299.99m, 50, null, null);
        var generalTicket = new CreateTicketTypeRequest("General", null, 99.99m, 450, null, null);

        var vip = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", vipTicket))!;
        var general = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", generalTicket))!;

        // Register 2 VIP and 3 General
        for (int i = 1; i <= 2; i++)
        {
            await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
                new CreateRegistrationRequest($"VIP{i}", "Test", $"vip{i}@example.com", null, vip.Id, null));
        }
        for (int i = 1; i <= 3; i++)
        {
            await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
                new CreateRegistrationRequest($"General{i}", "Test", $"general{i}@example.com", null, general.Id, null));
        }

        // Act - Export only VIP
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json?ticketTypeId={vip.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var attendees = JsonSerializer.Deserialize<List<AttendeeExportRecord>>(jsonContent, JsonOptions);

        attendees.Should().NotBeNull();
        attendees.Should().HaveCount(2);
        attendees.Should().AllSatisfy(a => a.TicketType.Should().Be("VIP"));
    }

    [Fact]
    public async Task ExportAttendees_WithSortByName_ShouldReturnSortedResults()
    {
        // Arrange
        var (eventResponse, _, _) = await CreateTestEventWithRegistrations();

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json?sortBy=name");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var attendees = JsonSerializer.Deserialize<List<AttendeeExportRecord>>(jsonContent, JsonOptions);

        attendees.Should().NotBeNull();
        attendees.Should().BeInAscendingOrder(a => a.AttendeeName);
    }

    [Fact]
    public async Task ExportAttendees_WithSortByRegistrationDate_ShouldReturnSortedResults()
    {
        // Arrange
        var (eventResponse, _, _) = await CreateTestEventWithRegistrations();

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json?sortBy=registrationDate");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var attendees = JsonSerializer.Deserialize<List<AttendeeExportRecord>>(jsonContent, JsonOptions);

        attendees.Should().NotBeNull();
        attendees.Should().BeInAscendingOrder(a => a.RegistrationDate);
    }

    [Fact]
    public async Task ExportAttendees_ShouldIncludeAllRequiredFields()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Complete Export Test", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500, null, "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("Premium", null, 150.00m, 100, null, null);
        var ticketResponse = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticketRequest))!;

        // Create discount code
        var discountRequest = new CreateDiscountCodeRequest("DISCOUNT10", "Percentage", 10m, null, DateTime.UtcNow, eventResponse.StartDate, null);
        await PostAsync<DiscountCodeResponse>($"/api/events/{eventResponse.Id}/discount-codes", discountRequest);

        // Register with discount
        var registration = new CreateRegistrationRequest("Complete", "User", "complete@example.com", "+1234567890", ticketResponse.Id, "DISCOUNT10");
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var attendees = JsonSerializer.Deserialize<List<AttendeeExportRecord>>(jsonContent, JsonOptions);

        attendees.Should().NotBeNull();
        attendees.Should().HaveCount(1);

        var attendee = attendees!.First();
        attendee.RegistrationId.Should().BeGreaterThan(0);
        attendee.AttendeeName.Should().Be("Complete User");
        attendee.Email.Should().Be("complete@example.com");
        attendee.PhoneNumber.Should().Be("+1234567890");
        attendee.TicketType.Should().Be("Premium");
        attendee.RegistrationDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        attendee.AmountPaid.Should().Be(135.00m); // 150 - 10% = 135
        attendee.Status.Should().Be("Confirmed");
        attendee.DiscountCodeUsed.Should().Be("DISCOUNT10");
    }

    [Fact]
    public async Task ExportAttendees_ForEventWithNoRegistrations_ShouldReturnEmptyList()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Empty Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500, null, "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("General", null, 99.99m, 100, null, null);
        await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticketRequest);

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var attendees = JsonSerializer.Deserialize<List<AttendeeExportRecord>>(jsonContent, JsonOptions);

        attendees.Should().NotBeNull();
        attendees.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAttendees_ForInvalidEventId_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/events/999999/export/json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportAttendees_CsvFormat_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Special Chars Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500, null, "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var ticketRequest = new CreateTicketTypeRequest("General", null, 99.99m, 100, null, null);
        var ticketResponse = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", ticketRequest))!;

        // Register with special characters in name
        var registration = new CreateRegistrationRequest("O'Brien", "Smith, Jr.", "obrien@example.com", null, ticketResponse.Id, null);
        await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations", registration);

        // Act
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/csv");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var csvContent = await response.Content.ReadAsStringAsync();
        csvContent.Should().Contain("O'Brien");
        csvContent.Should().Contain("Smith, Jr."); // Should properly escape commas
    }

    [Fact]
    public async Task ExportAttendees_ShouldSetCorrectContentDispositionHeader()
    {
        // Arrange
        var (eventResponse, _, _) = await CreateTestEventWithRegistrations();

        // Act
        var csvResponse = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/csv");
        var jsonResponse = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json");
        var excelResponse = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/excel");

        // Assert
        csvResponse.Content.Headers.ContentDisposition?.FileName.Should().Contain(".csv");
        jsonResponse.Content.Headers.ContentDisposition?.FileName.Should().Contain(".json");
        excelResponse.Content.Headers.ContentDisposition?.FileName.Should().Contain(".xlsx");
    }

    [Fact]
    public async Task ExportAttendees_WithMultipleFilters_ShouldApplyAllFilters()
    {
        // Arrange
        var eventRequest = new CreateEventRequest(
            "Multi-Filter Event", null, null, null,
            DateTime.UtcNow.AddMonths(2),
            DateTime.UtcNow.AddMonths(2).AddDays(2),
            500, null, "Published"
        );
        var eventResponse = (await PostAsync<EventResponse>("/api/events", eventRequest))!;

        var vipTicket = new CreateTicketTypeRequest("VIP", null, 299.99m, 50, null, null);
        var vip = (await PostAsync<TicketTypeResponse>($"/api/events/{eventResponse.Id}/ticket-types", vipTicket))!;

        // Register multiple VIP attendees
        for (int i = 1; i <= 3; i++)
        {
            await PostAsync<RegistrationResponse>($"/api/events/{eventResponse.Id}/registrations",
                new CreateRegistrationRequest($"VIP{i}", "Test", $"vip{i}@example.com", null, vip.Id, null));
        }

        // Cancel one
        var registrations = await GetAsync<List<RegistrationResponse>>($"/api/events/{eventResponse.Id}/registrations");
        await DeleteAsync($"/api/registrations/{registrations!.First().Id}");

        // Act - Export VIP + Confirmed + Sorted by name
        var response = await Client.GetAsync($"/api/events/{eventResponse.Id}/export/json?ticketTypeId={vip.Id}&status=Confirmed&sortBy=name");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var attendees = JsonSerializer.Deserialize<List<AttendeeExportRecord>>(jsonContent, JsonOptions);

        attendees.Should().NotBeNull();
        attendees.Should().HaveCount(2); // 3 VIP - 1 cancelled
        attendees.Should().AllSatisfy(a =>
        {
            a.TicketType.Should().Be("VIP");
            a.Status.Should().Be("Confirmed");
        });
        attendees.Should().BeInAscendingOrder(a => a.AttendeeName);
    }
}
