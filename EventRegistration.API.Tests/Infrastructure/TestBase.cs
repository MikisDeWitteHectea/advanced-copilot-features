using System.Net.Http.Json;
using System.Text.Json;

namespace EventRegistration.API.Tests.Infrastructure;

public abstract class TestBase : IClassFixture<EventRegistrationApiFactory>
{
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected TestBase(EventRegistrationApiFactory factory)
    {
        Client = factory.CreateClient();
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    protected async Task<TResponse?> PostAsync<TResponse>(string url, object request)
    {
        var response = await Client.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
    }

    protected async Task<TResponse?> GetAsync<TResponse>(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
    }

    protected async Task<TResponse?> PutAsync<TResponse>(string url, object request)
    {
        var response = await Client.PutAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
    }

    protected async Task DeleteAsync(string url)
    {
        var response = await Client.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }
}
