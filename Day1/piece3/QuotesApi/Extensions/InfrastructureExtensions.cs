using Microsoft.EntityFrameworkCore;
<<<<<<< HEAD
using QuotesApi.Data;
using QuotesApi.Middleware;
=======
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;
using QuotesApi.Repositories;
>>>>>>> a5d2af3cb7f84b071e8774aec7f2404d4ac2c1ab

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
<<<<<<< HEAD
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<QuoteDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=quotes.db"));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
    }
}
=======
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString("Default")));

        services.AddScoped<IQuoteRepository, QuoteRepository>();

        return services;
    }
}
>>>>>>> a5d2af3cb7f84b071e8774aec7f2404d4ac2c1ab
