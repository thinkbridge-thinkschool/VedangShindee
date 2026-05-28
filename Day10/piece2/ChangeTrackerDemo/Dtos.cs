namespace ChangeTrackerDemo;

public record ProductSummaryDto(int Id, string Name, string Category);
public record OrderSummaryDto(int Id, string CustomerName, int ItemCount);
