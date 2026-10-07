using EventRegistration.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Threading.RateLimiting;

namespace EventRegistration.API.Tests.EndToEndTests;

public class RateLimitingTests
{
    [Fact]
    public async Task GlobalLimiter_WhenLimitIsExceeded_ShouldReturnTooManyRequests()
    {
        await using var factory = new EventRegistrationApiFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<RateLimiterOptions>(options =>
                    {
                        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                            RateLimitPartition.GetFixedWindowLimiter(
                                "test-client",
                                _ => new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit = 2,
                                    Window = TimeSpan.FromMinutes(1),
                                    QueueLimit = 0,
                                    AutoReplenishment = true
                                }));
                    });
                });
            });
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/events")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/events")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
    }
}
