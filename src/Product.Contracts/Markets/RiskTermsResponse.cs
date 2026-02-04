namespace Product.Contracts.Markets;

public class RiskTermsResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MarketId { get; set; }
    public string TermVersion { get; set; } = string.Empty;
    public DateTimeOffset AcceptedAt { get; set; }
    public string? TermSnapshot { get; set; }
    public string? TermHash { get; set; }
}
