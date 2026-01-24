namespace Product.Contracts.Markets;

public class BuyResponse
{
    public int Contracts { get; set; }
    public decimal PricePerContract { get; set; }
    public decimal Spent { get; set; }
    public decimal Fee { get; set; }
    public decimal NewBalance { get; set; }
    public Guid PositionId { get; set; }
}
