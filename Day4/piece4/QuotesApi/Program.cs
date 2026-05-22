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

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    app.UseExceptionHandler();

    // Push TraceId into every log line for the duration of the request.
    app.Use((ctx, next) =>
    {
        using (LogContext.PushProperty("TraceId", ctx.TraceIdentifier))
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
