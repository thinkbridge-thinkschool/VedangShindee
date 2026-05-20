using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
<<<<<<< HEAD
using QuotesApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

// Apply EF Core migrations at startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuoteDbContext>();
    dbContext.Database.Migrate();
}

app.MapQuoteEndpoints();

app.Run();
=======
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
>>>>>>> a5d2af3cb7f84b071e8774aec7f2404d4ac2c1ab
