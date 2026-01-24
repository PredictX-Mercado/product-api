using Product.Common.Entities;

namespace Product.Data.Models.Webhooks;

public class MPWebhookEvent : Entity<Guid>
{
    public string Provider { get; set; } = string.Empty; // e.g. "mercadopago"
    public string EventType { get; set; } = string.Empty; // e.g. "payment"
    public long? ProviderPaymentId { get; set; }
    public string? OrderId { get; set; }
    public string Payload { get; set; } = string.Empty; // raw json
    public string? Headers { get; set; }
    public string? SignatureHeader { get; set; }
    public int? ResponseStatusCode { get; set; }
    public int? ProcessingDurationMs { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Processed { get; set; } = false;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int AttemptCount { get; set; } = 0;
    public string? ProcessingResult { get; set; }
}
