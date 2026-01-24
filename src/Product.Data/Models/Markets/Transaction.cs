using Product.Common.Entities;

namespace Product.Data.Models.Markets;

public class Transaction : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = null!; // buy/sell/fee
    public decimal Amount { get; set; }
    public decimal NetAmount { get; set; }
    public Guid? MarketId { get; set; }
    public string? Description { get; set; }
}
