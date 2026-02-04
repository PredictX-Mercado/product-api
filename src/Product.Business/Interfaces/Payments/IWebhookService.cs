using Product.Data.Models.Webhooks;

namespace Product.Business.Interfaces.Payments;

public interface IWebhookService
{
    Task<MPWebhookEvent> SaveAsync(
        string provider,
        string eventType,
        long? providerPaymentId,
        string? orderId,
        string payload,
        string? headers,
        CancellationToken ct = default
    );

    Task<MPWebhookEvent?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    );

    Task MarkProcessedAsync(
        Guid id,
        bool processed,
        string? result,
        string? orderId = null,
        int? responseStatusCode = null,
        int? processingDurationMs = null,
        CancellationToken ct = default
    );

    Task<int> CleanupUnprocessedAsync(int take, CancellationToken ct = default);
}
