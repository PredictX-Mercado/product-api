using Product.Common.Entities;

namespace Product.Data.Models.Wallet;

public class Receipt : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty; // deposit|withdraw|buy
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string? Provider { get; set; }
    public long? ProviderPaymentId { get; set; }
    public string? ProviderPaymentIdText { get; set; }
    public string? ExternalPaymentId { get; set; }
    public Guid? PaymentIntentId { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTimeOffset? PaymentExpiresAt { get; set; }
    public string? CheckoutUrl { get; set; }
    public Guid? MarketId { get; set; }
    public string? MarketTitleSnapshot { get; set; }
    public string? MarketSlugSnapshot { get; set; }
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? PayloadJson { get; set; }
}
