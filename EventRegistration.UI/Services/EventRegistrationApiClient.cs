using EventRegistration.UI.Models;
using System.Net.Http.Json;

namespace EventRegistration.UI.Services;

public class EventRegistrationApiClient
{
    private readonly HttpClient _httpClient;

    public EventRegistrationApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Events
    public async Task<List<EventResponse>> GetEventsAsync(string? status = null)
    {
        var url = status != null ? $"api/events?status={status}" : "api/events";
        return await _httpClient.GetFromJsonAsync<List<EventResponse>>(url) ?? new();
    }

    public async Task<EventResponse?> GetEventAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<EventResponse>($"api/events/{id}");
    }

    public async Task<EventResponse?> CreateEventAsync(CreateEventRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/events", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventResponse>();
    }

    public async Task DeleteEventAsync(int id)
    {
        await _httpClient.DeleteAsync($"api/events/{id}");
    }

    // Ticket Types
    public async Task<List<TicketTypeResponse>> GetTicketTypesAsync(int eventId)
    {
        return await _httpClient.GetFromJsonAsync<List<TicketTypeResponse>>($"api/events/{eventId}/ticket-types") ?? new();
    }

    public async Task<TicketTypeResponse?> CreateTicketTypeAsync(int eventId, CreateTicketTypeRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/events/{eventId}/ticket-types", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TicketTypeResponse>();
    }

    public async Task DeleteTicketTypeAsync(int eventId, int ticketTypeId)
    {
        await _httpClient.DeleteAsync($"api/events/{eventId}/ticket-types/{ticketTypeId}");
    }

    // Registrations
    public async Task<List<RegistrationResponse>> GetRegistrationsAsync(int eventId)
    {
        return await _httpClient.GetFromJsonAsync<List<RegistrationResponse>>($"api/events/{eventId}/registrations") ?? new();
    }

    public async Task<RegistrationResponse?> CreateRegistrationAsync(int eventId, CreateRegistrationRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/events/{eventId}/registrations", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RegistrationResponse>();
    }

    public async Task CancelRegistrationAsync(int registrationId)
    {
        await _httpClient.DeleteAsync($"api/registrations/{registrationId}");
    }

    // Capacity
    public async Task<CapacityResponse?> GetCapacityAsync(int eventId)
    {
        return await _httpClient.GetFromJsonAsync<CapacityResponse>($"api/events/{eventId}/capacity");
    }

    // Waitlist
    public async Task<List<WaitlistEntryResponse>> GetWaitlistAsync(int eventId)
    {
        return await _httpClient.GetFromJsonAsync<List<WaitlistEntryResponse>>($"api/events/{eventId}/waitlist") ?? new();
    }

    public async Task RemoveFromWaitlistAsync(int waitlistId)
    {
        await _httpClient.DeleteAsync($"api/waitlist/{waitlistId}");
    }

    // Discount Codes
    public async Task<List<DiscountCodeResponse>> GetDiscountCodesAsync(int eventId)
    {
        return await _httpClient.GetFromJsonAsync<List<DiscountCodeResponse>>($"api/events/{eventId}/discount-codes") ?? new();
    }

    public async Task<DiscountCodeResponse?> CreateDiscountCodeAsync(int eventId, CreateDiscountCodeRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/events/{eventId}/discount-codes", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscountCodeResponse>();
    }

    public async Task DeleteDiscountCodeAsync(int discountCodeId)
    {
        await _httpClient.DeleteAsync($"api/discount-codes/{discountCodeId}");
    }

    // Cancellation Policy
    public async Task<CancellationPolicyResponse?> GetCancellationPolicyAsync(int eventId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CancellationPolicyResponse>($"api/events/{eventId}/cancellation-policy");
        }
        catch
        {
            return null;
        }
    }

    public async Task<CancellationPolicyResponse?> CreateCancellationPolicyAsync(int eventId, CreateCancellationPolicyRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/events/{eventId}/cancellation-policy", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CancellationPolicyResponse>();
    }

    public async Task<CancellationPolicyResponse?> UpdateCancellationPolicyAsync(int eventId, CreateCancellationPolicyRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/events/{eventId}/cancellation-policy", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CancellationPolicyResponse>();
    }

    // Export
    public async Task<byte[]> ExportAttendeesAsync(int eventId, string format, string? status = null, string? sortBy = null)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={status}");
        if (!string.IsNullOrEmpty(sortBy)) queryParams.Add($"sortBy={sortBy}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var response = await _httpClient.GetAsync($"api/events/{eventId}/export/{format}{query}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }
}
