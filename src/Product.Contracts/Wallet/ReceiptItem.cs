namespace Product.Contracts.Wallet;

public class ReceiptItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Provider { get; set; }
    public long? ProviderPaymentId { get; set; }
    public string? ProviderPaymentIdText { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? MetaJson { get; set; }
}
