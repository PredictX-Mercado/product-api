using Product.Common.Entities;

namespace Product.Data.Models.Markets;

public class Position : Entity<Guid>
{
    public Guid MarketId { get; set; }
    public Guid UserId { get; set; }
    public string Side { get; set; } = null!; // yes|no
    public int Contracts { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal TotalInvested { get; set; }
    public string Status { get; set; } = "open";
}
