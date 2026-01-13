namespace Product.Contracts.Payments;

public class CreatePixRequest
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public string BuyerEmail { get; set; } = default!;
    public int? ExpirationMinutes { get; set; }
}
