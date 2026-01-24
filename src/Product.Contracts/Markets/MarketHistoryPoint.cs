namespace Product.Contracts.Markets;

public class MarketHistoryPoint
{
    public DateTimeOffset Timestamp { get; set; }
    public decimal YesPrice { get; set; }
    public decimal NoPrice { get; set; }
    public decimal Volume { get; set; }
}
