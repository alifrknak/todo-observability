using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// 1. EF Core SQLite Registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=todos.db"));

// 2. HttpClient Registration
builder.Services.AddHttpClient();

// 3. OpenTelemetry Metrics Configuration
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("TodoService"))
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("Microsoft.EntityFrameworkCore")
            .AddMeter("TodoService.Custom")
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

var app = builder.Build();

// Auto-create SQLite database schema on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

// Expose /metrics endpoint for Prometheus to scrape
app.MapPrometheusScrapingEndpoint();

app.UseAuthorization();
app.MapControllers();

app.Run();
