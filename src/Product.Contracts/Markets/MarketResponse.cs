using Product.Common.Entities;

namespace Product.Contracts.Markets;

public class MarketResponse : Entity<Guid>
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTimeOffset? ClosingDate { get; set; }
    public string? ResolutionSource { get; set; }
    public string Status { get; set; } = null!;
    public bool Featured { get; set; }
    public decimal YesPrice { get; set; }
    public decimal NoPrice { get; set; }
    public decimal VolumeTotal { get; set; }
    public int YesContracts { get; set; }
    public int NoContracts { get; set; }
    public decimal ProbabilityYes => Math.Round(YesPrice * 100, 2);
}
