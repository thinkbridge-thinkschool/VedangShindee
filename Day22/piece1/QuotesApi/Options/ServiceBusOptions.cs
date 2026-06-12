namespace QuotesApi.Options;

public sealed class ServiceBusOptions
{
    public string ConnectionString { get; set; } = "";
    public string TopicName { get; set; } = "quotes";
    public string EmailSubscriptionName { get; set; } = "email-notifications";
    public string AuditSubscriptionName { get; set; } = "audit-log";

    // Number of concurrent handlers per subscription processor.
    // This drives the competing-consumer pattern: the SDK will call HandleMessageAsync
    // on up to MaxConcurrentCalls goroutines simultaneously from the same subscription.
    public int MaxConcurrentCalls { get; set; } = 2;
}
