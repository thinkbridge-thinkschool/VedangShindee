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
            // 20 authors × 5 quotes = 100 rows — enough for N+1 to be measurable under load.
            // Deliberately no index on Quotes.Author to trigger full-table-scan per author lookup.
            db.Quotes.AddRange(
                new Quote { Author = "Seneca", Text = "Luck is what happens when preparation meets opportunity.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Seneca", Text = "Difficulties strengthen the mind, as labor does the body.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Seneca", Text = "It is not that I am brave, it is just that I am busy.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Seneca", Text = "Begin at once to live, and count each separate day as a separate life.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Seneca", Text = "He suffers more than necessary, who suffers before it is necessary.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Marcus Aurelius", Text = "You have power over your mind, not outside events. Realize this, and you will find strength.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Marcus Aurelius", Text = "The impediment to action advances action. What stands in the way becomes the way.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Marcus Aurelius", Text = "Waste no more time arguing what a good man should be. Be one.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Marcus Aurelius", Text = "Accept the things to which fate binds you.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Marcus Aurelius", Text = "Never let the future disturb you. You will meet it, if you have to, with the same weapons of reason.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Epictetus", Text = "Make the best use of what is in your power, and take the rest as it happens.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Epictetus", Text = "He is a wise man who does not grieve for the things which he has not, but rejoices for those which he has.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Epictetus", Text = "First say to yourself what you would be; and then do what you have to do.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Epictetus", Text = "Men are disturbed not by things, but by the opinions about things.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Epictetus", Text = "Seek not the good in external things; seek it in yourself.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Aristotle", Text = "We are what we repeatedly do. Excellence, then, is not an act, but a habit.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Aristotle", Text = "Knowing yourself is the beginning of all wisdom.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Aristotle", Text = "The more you know, the more you know you don't know.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Aristotle", Text = "It is the mark of an educated mind to entertain a thought without accepting it.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Aristotle", Text = "A friend to all is a friend to none.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Plato", Text = "The beginning is the most important part of the work.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Plato", Text = "Good actions give strength to ourselves and inspire good actions in others.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Plato", Text = "Wise men speak because they have something to say; fools because they have to say something.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Plato", Text = "We can easily forgive a child who is afraid of the dark.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Plato", Text = "The measure of a man is what he does with power.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Socrates", Text = "The unexamined life is not worth living.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Socrates", Text = "I know that I know nothing.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Socrates", Text = "Education is the kindling of a flame, not the filling of a vessel.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Socrates", Text = "Wonder is the beginning of wisdom.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Socrates", Text = "To find yourself, think for yourself.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Confucius", Text = "It does not matter how slowly you go as long as you do not stop.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Confucius", Text = "Life is really simple, but we insist on making it complicated.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Confucius", Text = "He who learns but does not think, is lost.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Confucius", Text = "Our greatest glory is not in never falling, but in rising every time we fall.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Confucius", Text = "The man who moves a mountain begins by carrying away small stones.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Lao Tzu", Text = "A journey of a thousand miles begins with a single step.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Lao Tzu", Text = "Knowing others is wisdom. Knowing yourself is enlightenment.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Lao Tzu", Text = "Be careful what you water your dreams with.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Lao Tzu", Text = "Nature does not hurry, yet everything is accomplished.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Lao Tzu", Text = "Silence is a source of great strength.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Sun Tzu", Text = "The supreme art of war is to subdue the enemy without fighting.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Sun Tzu", Text = "In the midst of chaos, there is also opportunity.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Sun Tzu", Text = "The greatest victory is that which requires no battle.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Sun Tzu", Text = "Know yourself and you will win all battles.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Sun Tzu", Text = "Opportunities multiply as they are seized.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Nietzsche", Text = "That which does not kill us, makes us stronger.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Nietzsche", Text = "Without music, life would be a mistake.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Nietzsche", Text = "He who has a why to live can bear almost any how.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Nietzsche", Text = "It is not a lack of love, but a lack of friendship that makes unhappy marriages.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Nietzsche", Text = "One must still have chaos in oneself to be able to give birth to a dancing star.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Kant", Text = "Act only according to that maxim whereby you can at the same time will it to be a universal law.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Kant", Text = "We are not rich by what we possess but by what we can do without.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Kant", Text = "Science is organized knowledge. Wisdom is organized life.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Kant", Text = "Happiness is not an ideal of reason, but of imagination.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Kant", Text = "Two things awe me most, the starry sky above me and the moral law within me.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Descartes", Text = "I think, therefore I am.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Descartes", Text = "The reading of all good books is like a conversation with the finest minds of past centuries.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Descartes", Text = "Divide each difficulty into as many parts as is feasible.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Descartes", Text = "It is not enough to have a good mind; the main thing is to use it well.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Descartes", Text = "Conquer yourself rather than the world.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Voltaire", Text = "Judge a man by his questions rather than by his answers.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Voltaire", Text = "Each player must accept the cards life deals him.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Voltaire", Text = "The more I read, the more I meditate; and the more I meditate, the more I am satisfied that I know nothing.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Voltaire", Text = "No problem can withstand the assault of sustained thinking.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Voltaire", Text = "Perfect is the enemy of good.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Hume", Text = "Reason is, and ought only to be the slave of the passions.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Hume", Text = "Beauty is no quality in things themselves; it exists merely in the mind which contemplates them.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Hume", Text = "A wise man proportions his belief to the evidence.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Hume", Text = "Custom is the great guide of human life.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Hume", Text = "The life of man is of no greater importance to the universe than that of an oyster.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Locke", Text = "Reading furnishes the mind only with materials of knowledge; it is thinking that makes what we read ours.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Locke", Text = "The end of law is not to abolish or restrain, but to preserve and enlarge freedom.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Locke", Text = "Education begins the gentleman, but reading, good company and reflection must finish him.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Locke", Text = "New opinions are always suspected, and usually opposed, without any other reason but because they are not already common.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Locke", Text = "All mankind being all equal and independent, no one ought to harm another in his life, health, liberty, or possessions.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Rousseau", Text = "People who know little are usually great talkers, while men who know much say little.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Rousseau", Text = "Man is born free, and everywhere he is in chains.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Rousseau", Text = "The world of reality has its limits; the world of imagination is boundless.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Rousseau", Text = "Patience is bitter, but its fruit is sweet.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Rousseau", Text = "Do not judge, and you will never be mistaken.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Spinoza", Text = "Peace is not the absence of conflict, but the ability to handle conflict by peaceful means.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Spinoza", Text = "Do not weep; do not wax indignant. Understand.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Spinoza", Text = "The highest activity a human being can attain is learning for understanding.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Spinoza", Text = "I have made a ceaseless effort not to ridicule, not to bewail, nor to scorn human actions, but to understand them.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Spinoza", Text = "Freedom is absolutely necessary for the progress in science and the liberal arts.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Leibniz", Text = "Music is the pleasure the human mind experiences from counting without being aware that it is counting.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Leibniz", Text = "The present is the child of the past; but the past of the present is an even more wonderful child.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Leibniz", Text = "If God did not exist, it would be necessary to invent Him.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Leibniz", Text = "Take what you need, do what you should, you will get what you want.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Leibniz", Text = "The art of discovering the causes of phenomena and of explaining them.", OwnerId = userId, CreatedAt = now },

                new Quote { Author = "Pascal", Text = "All of humanity's problems stem from man's inability to sit quietly in a room alone.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Pascal", Text = "The heart has its reasons which reason knows not.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Pascal", Text = "In faith there is enough light for those who want to believe and enough shadows to blind those who don't.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Pascal", Text = "Man's greatness lies in his power of thought.", OwnerId = userId, CreatedAt = now },
                new Quote { Author = "Pascal", Text = "Kind words do not cost much. Yet they accomplish much.", OwnerId = userId, CreatedAt = now }
            );
            db.SaveChanges();
        }
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAuthEndpoints();
    app.MapQuoteEndpoints();
    app.MapSlowAuthorEndpoints();

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
