using QuotesApi.Data;
using QuotesApi.Endpoints;
using QuotesApi.Extensions;
using QuotesApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

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

// Needed so WebApplicationFactory<Program> in integration tests can reference this type.
public partial class Program { }
