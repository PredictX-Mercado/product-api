namespace Product.Contracts.Wallet;

public class ReceiptItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string? Provider { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Description { get; set; }
    public int? Contracts { get; set; }
    public decimal? UnitPrice { get; set; }
    public ReceiptMarket? Market { get; set; }
    public ReceiptPayment? Payment { get; set; }
}

public class ReceiptMarket
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
}

public class ReceiptPayment
{
    public string? Method { get; set; }
    public string? ExternalPaymentId { get; set; }
    public string? QrCodeBase64 { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
