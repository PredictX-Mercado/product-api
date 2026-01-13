namespace Product.Contracts.Payments;

public class ChargeSavedRequest
{
    public Guid PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public string BuyerEmail { get; set; } = default!;
    public int? Installments { get; set; }
}
