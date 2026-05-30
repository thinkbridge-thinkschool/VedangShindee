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
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}"));

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

            // 20 authors × 10 quotes = 200 rows.
            // Author column has no index, so each per-author WHERE scan is a full table scan.
            var authorsAndQuotes = new (string Author, string[] Texts)[]
            {
                ("Seneca", ["Luck is what happens when preparation meets opportunity.", "It is not that I am brave, it is just that I am busy.", "We suffer more often in imagination than in reality.", "No person has the power to have everything they want, but it is in their power not to want what they don't have.", "Difficulties strengthen the mind, as labour does the body.", "Begin at once to live, and count each separate day as a separate life.", "He who is brave is free.", "If you really want to escape the things that harass you, what you're needing is not to be in a different place but to be a different person.", "True happiness is to enjoy the present.", "Waste no more time arguing what a good man should be."]),
                ("Marcus Aurelius", ["You have power over your mind, not outside events.", "The impediment to action advances action. What stands in the way becomes the way.", "Confine yourself to the present.", "Very little is needed to make a happy life.", "If it is not right, do not do it; if it is not true, do not say it.", "The best revenge is not to be like your enemy.", "Never esteem anything as of advantage if it will make you break your word.", "When you wake up in the morning, tell yourself: the people I deal with today will be meddling.", "Accept the things to which fate binds you.", "Do not indulge in dreams of what you do not have."]),
                ("Epictetus", ["Make the best use of what is in your power, and take the rest as it happens.", "Wealth consists not in having great possessions, but in having few wants.", "He is a wise man who does not grieve for the things which he has not, but rejoices for those which he has.", "No man is free who is not master of himself.", "First say to yourself what you would be; and then do what you have to do.", "Men are disturbed not by things, but by the opinion about things.", "Only the educated are free.", "It's not what happens to you, but how you react to it that matters.", "Seek not the good in external things; seek it in yourself.", "You are a little soul carrying around a corpse."]),
                ("Aristotle", ["We are what we repeatedly do. Excellence, then, is not an act, but a habit.", "Knowing yourself is the beginning of all wisdom.", "The more you know, the more you know you don't know.", "Happiness depends upon ourselves.", "In all things of nature there is something of the marvelous.", "It is during our darkest moments that we must focus to see the light.", "Patience is bitter, but its fruit is sweet.", "The secret to humor is surprise.", "To perceive is to suffer.", "Quality is not an act, it is a habit."]),
                ("Plato", ["The beginning is the most important part of the work.", "Wise men speak because they have something to say; fools because they have to say something.", "At the touch of love everyone becomes a poet.", "Every heart sings a song, incomplete, until another heart whispers back.", "The measure of a man is what he does with power.", "Human behavior flows from three main sources: desire, emotion, and knowledge.", "Courage is knowing what not to fear.", "Good actions give strength to ourselves and inspire good actions in others.", "Beauty lies in the eyes of the beholder.", "The price good men pay for indifference to public affairs is to be ruled by evil men."]),
                ("Socrates", ["The only true wisdom is in knowing you know nothing.", "An unexamined life is not worth living.", "Education is the kindling of a flame, not the filling of a vessel.", "To find yourself, think for yourself.", "The secret of happiness is not found in seeking more, but in developing the capacity to enjoy less.", "He who is not contented with what he has, would not be contented with what he would like to have.", "From the deepest desires often come the deadliest hate.", "Be slow to fall into friendship but when thou art in, continue firm and constant.", "Beware the barrenness of a busy life.", "I know that I am intelligent because I know that I know nothing."]),
                ("Confucius", ["It does not matter how slowly you go as long as you do not stop.", "Life is really simple, but we insist on making it complicated.", "Everything has beauty, but not everyone sees it.", "Our greatest glory is not in never falling, but in rising every time we fall.", "He who learns but does not think, is lost. He who thinks but does not learn is in great danger.", "The superior man is satisfied and composed; the mean man is always full of distress.", "When it is obvious that the goals cannot be reached, don't adjust the goals, adjust the action steps.", "Wherever you go, go with all your heart.", "I hear and I forget. I see and I remember. I do and I understand.", "By three methods we may learn wisdom: reflection, imitation, and experience."]),
                ("Lao Tzu", ["The journey of a thousand miles begins with a single step.", "Nature does not hurry, yet everything is accomplished.", "When I let go of what I am, I become what I might be.", "The key to growth is the introduction of higher dimensions of consciousness into our awareness.", "To the mind that is still, the whole universe surrenders.", "Life is a series of natural and spontaneous changes.", "Silence is a source of great strength.", "Be careful what you water your dreams with.", "A good traveler has no fixed plans and is not intent on arriving.", "Health is the greatest possession."]),
                ("Sun Tzu", ["The supreme art of war is to subdue the enemy without fighting.", "Appear weak when you are strong, and strong when you are weak.", "In the midst of chaos, there is also opportunity.", "He will win who knows when to fight and when not to fight.", "All warfare is based on deception.", "Victorious warriors win first and then go to war, while defeated warriors go to war first.", "Supreme excellence consists in breaking the enemy's resistance without fighting.", "If you know the enemy and know yourself, you need not fear the result of a hundred battles.", "Move swift as the Wind and closely-formed as the Wood.", "When you surround an army, leave an outlet free."]),
                ("Nietzsche", ["That which does not kill us makes us stronger.", "Without music, life would be a mistake.", "It is not a lack of love, but a lack of friendship that makes unhappy marriages.", "In individuals, insanity is rare; but in groups, parties, nations and epochs, it is the rule.", "The higher we soar, the smaller we appear to those who cannot fly.", "There are no facts, only interpretations.", "We should consider every day lost on which we have not danced at least once.", "One must still have chaos in oneself to be able to give birth to a dancing star.", "He who has a why to live can bear almost any how.", "You must have chaos within you to give birth to a dancing star."]),
                ("Voltaire", ["Judge a man by his questions rather than his answers.", "The more I read, the more I acquire, the more certain I am that I know nothing.", "Common sense is not so common.", "Each player must accept the cards life deals him or her; but once they are in hand, he or she alone must decide how to play.", "God is a comedian playing to an audience too afraid to laugh.", "Life is a shipwreck, but we must not forget to sing in the lifeboats.", "To hold a pen is to be at war.", "It is dangerous to be right in matters on which the established authorities are wrong.", "Every man is guilty of all the good he did not do.", "Think for yourself and let others enjoy the privilege of doing so too."]),
                ("Descartes", ["I think, therefore I am.", "The reading of all good books is like conversation with the finest minds of past centuries.", "It is not enough to have a good mind; the main thing is to use it well.", "Divide each difficulty into as many parts as is feasible and necessary to resolve it.", "The greatest minds are capable of the greatest vices as well as of the greatest virtues.", "Conquer yourself rather than the world.", "In order to improve the mind, we ought less to learn, than to contemplate.", "A state is better governed which has but few laws, and those laws strictly observed.", "Nothing is more fairly distributed than common sense.", "Except our own thoughts, there is nothing absolutely in our power."]),
                ("Kant", ["Act only according to that maxim whereby you can at the same time will that it should become a universal law.", "Two things awe me most: the starry sky above me and the moral law within me.", "We are not rich by what we possess but by what we can do without.", "Morality is not the doctrine of how we may make ourselves happy, but how we may make ourselves worthy of happiness.", "The only objects of practical reason are therefore those of good and evil.", "Experience without theory is blind, but theory without experience is mere intellectual play.", "Happiness is not an ideal of reason, but of imagination.", "A good will is not good because of what it effects, or accomplishes.", "All our knowledge begins with the senses, proceeds then to the understanding.", "If you punish a child for being naughty and reward him for being good, he will do right merely for the sake of the reward."]),
                ("Hegel", ["We learn from history that we do not learn from history.", "Nothing great in the world was accomplished without passion.", "The truth is the whole.", "Genuine tragedies in the world are not conflicts between right and wrong.", "The most thought-provoking thing in our thought-provoking time is that we are still not thinking.", "To be independent of public opinion is the first formal condition of achieving anything great.", "What experience and history teach us is this: that people and governments have never learned anything from history.", "Education is the art of making man ethical.", "An idea is always a generalization, and generalization is a property of thinking.", "The courage of the truth is the first condition of philosophic study."]),
                ("Schopenhauer", ["Talent hits a target no one else can hit; Genius hits a target no one else can see.", "A man can be himself only so long as he is alone.", "Reading is equivalent to thinking with someone else's head.", "The world is my idea.", "To live alone is the fate of all great souls.", "Almost all of our sorrows spring out of our relations with other people.", "Money is human happiness in the abstract.", "It is a clear gain to sacrifice pleasure in order to avoid pain.", "Compassion is the basis of morality.", "The more unintelligent a man is, the less mysterious existence seems to him."]),
                ("Kierkegaard", ["Life can only be understood backwards; but it must be lived forwards.", "The most common form of despair is not being who you are.", "To dare is to lose one's footing momentarily. Not to dare is to lose oneself.", "Once you label me you negate me.", "Anxiety is the dizziness of freedom.", "Face the facts of being what you are, for that is what changes what you are.", "Don't forget to love yourself.", "The self is only that which it is in the process of becoming.", "People commonly travel the world over to see rivers and mountains.", "Above all, do not lose your desire to walk."]),
                ("Sartre", ["Existence precedes essence.", "Hell is other people.", "Man is condemned to be free.", "Every existing thing is born without reason, prolongs itself out of weakness.", "We are our choices.", "Life begins on the other side of despair.", "If you are lonely when you're alone, you are in bad company.", "Freedom is what you do with what's been done to you.", "One always dies too soon or too late. And yet life is there, finished.", "Man is not the sum of what he has already, but rather the sum of what he does not yet have."]),
                ("Camus", ["In the depth of winter, I finally learned that within me there lay an invincible summer.", "You will never be happy if you continue to search for what happiness consists of.", "Don't walk behind me; I may not lead. Don't walk in front of me; I may not follow. Just walk beside me.", "The absurd is the essential concept and the first truth.", "An intellectual is someone whose mind watches itself.", "I would rather live my life as if there is a God and die to find out there isn't.", "The only way to deal with an unfree world is to become so absolutely free.", "Always go too far, because that's where you'll find the truth.", "Man is the only creature who refuses to be what he is.", "There is but one truly serious philosophical problem, and that is suicide."]),
                ("Wittgenstein", ["Whereof one cannot speak, thereof one must be silent.", "If a lion could talk, we could not understand him.", "The limits of my language mean the limits of my world.", "A serious and good philosophical work could be written consisting entirely of jokes.", "The world is everything that is the case.", "Philosophy is a battle against the bewitchment of our intelligence by means of our language.", "What can be shown cannot be said.", "A man will be imprisoned in a room with a door that's unlocked and opens inwards.", "Nothing is so difficult as not deceiving oneself.", "A philosophical problem has the form: I don't know my way about."]),
                ("Bertrand Russell", ["The whole problem with the world is that fools and fanatics are always so certain of themselves.", "The good life is one inspired by love and guided by knowledge.", "Do not fear to be eccentric in opinion, for every opinion now accepted was once eccentric.", "The secret to happiness is to face the fact that the world is horrible.", "To conquer fear is the beginning of wisdom.", "One of the symptoms of an approaching nervous breakdown is the belief that one's work is terribly important.", "Most people would sooner die than think; in fact, they do so.", "War does not determine who is right — only who is left.", "The whole problem with the world is fools and fanatics are always certain.", "It is the preoccupation with possessions, more than anything else, that prevents us from living freely and nobly."]),
            };

            var allQuotes = authorsAndQuotes.SelectMany(a =>
                a.Texts.Select(text => new Quote
                {
                    Author = a.Author,
                    Text = text,
                    OwnerId = userId,
                    CreatedAt = now
                })).ToList();

            db.Quotes.AddRange(allQuotes);
            db.SaveChanges();
        }
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAuthEndpoints();
    app.MapQuoteEndpoints();
    app.MapAuthorReportEndpoints();

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
