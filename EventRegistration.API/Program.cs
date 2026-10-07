using EventRegistration.API.Data;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);

if (permitLimit <= 0 || windowSeconds <= 0)
{
    throw new InvalidOperationException(
        "RateLimiting:PermitLimit and RateLimiting:WindowSeconds must be greater than zero.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress ?? IPAddress.None,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out var leaseRetryAfter)
            ? leaseRetryAfter
            : TimeSpan.FromSeconds(windowSeconds);

        context.HttpContext.Response.Headers.RetryAfter =
            Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                status = StatusCodes.Status429TooManyRequests,
                title = "Too many requests",
                detail = "The request rate limit has been exceeded. Try again later."
            },
            cancellationToken);
    };
});

// Configure DbContext - use InMemory for testing
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase("TestDb");
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=EventRegistration;Trusted_Connection=true;");
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program class accessible to tests
public partial class Program { }
