using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// 1. EF Core SQLite Registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=todos.db"));

// 2. HttpClient Registration
builder.Services.AddHttpClient();

// 3. OpenTelemetry Configuration
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("TodoService"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });

var app = builder.Build();

// Auto-create SQLite database schema on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
