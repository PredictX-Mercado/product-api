using Product.Common.Entities;
using Product.Common.Enums;
using Product.Data.Models.Users;

namespace Product.Data.Models.Wallet;

public class PaymentIntent : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string Provider { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.PENDING;
    public string? ExternalPaymentId { get; set; }
    public string? ExternalReference { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public string PaymentMethod { get; set; } = "pix"; // pix|card|other
    public string? PixQrCode { get; set; }
    public string? PixQrCodeBase64 { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public ApplicationUser? User { get; set; }
}
