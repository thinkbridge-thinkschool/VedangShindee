using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=quotes.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Database.EnsureCreated();
}

app.MapGet("/", () => "API WORKING");

app.MapGet("/test", async (AppDbContext db) =>
{
    return await db.Quotes.ToListAsync();
});

app.MapPost("/test", async (Quote quote, AppDbContext db) =>
{
    db.Quotes.Add(quote);

    await db.SaveChangesAsync();

    return Results.Ok(quote);
});

app.Run();