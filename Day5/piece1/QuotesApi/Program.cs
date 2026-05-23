using System.Diagnostics;
using Azure.Identity;
using QuotesApi.Data;
using QuotesApi.Endpoints;
using QuotesApi.Extensions;
using QuotesApi.Models;
using Serilog;
using Serilog.Context;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Key Vault is only used in non-Development environments (prod/staging).
    // Locally, put ApplicationInsights:ConnectionString in appsettings.Development.json instead.
    if (!builder.Environment.IsDevelopment())
    {
        var keyVaultUri = builder.Configuration["Azure:KeyVaultUri"];
        if (!string.IsNullOrEmpty(keyVaultUri))
            builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
    }

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

    var app = builder.Build();

    app.UseExceptionHandler();

    // Push OTel TraceId into every log line so logs and traces correlate by the same ID.
    app.Use((ctx, next) =>
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;
        using (LogContext.PushProperty("TraceId", traceId))
            return next();
    });

    app.UseSerilogRequestLogging();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
            });
            db.SaveChanges();
        }

        if (!db.Quotes.Any())
        {
            var userId = db.Users.First().Id;
            var now = DateTimeOffset.UtcNow;
            db.Quotes.AddRange(
                new Quote { Author = "Seneca", Text = "Luck is what happens when preparation meets opportunity.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Marcus Aurelius", Text = "You have power over your mind, not outside events. Realize this, and you will find strength.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Epictetus", Text = "Make the best use of what is in your power, and take the rest as it happens.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Aristotle", Text = "We are what we repeatedly do. Excellence, then, is not an act, but a habit.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Plato", Text = "The beginning is the most important part of the work.", OwnerId = userId, CreatedAt = now }
            );
            db.SaveChanges();
        }
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAuthEndpoints();
    app.MapQuoteEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Needed so WebApplicationFactory<Program> in integration tests can reference this type.
public partial class Program { }
