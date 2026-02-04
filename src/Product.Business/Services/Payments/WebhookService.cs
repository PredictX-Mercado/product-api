using Product.Business.Interfaces.Payments;
using Product.Common.Utilities;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Webhooks;

namespace Product.Business.Services.Payments;

public class WebhookService : IWebhookService
{
    private readonly IWebhookRepository _webhookRepository;
    private readonly IMercadoPagoRepository _mpRepository;

    public WebhookService(IWebhookRepository webhookRepository, IMercadoPagoRepository mpRepository)
    {
        _webhookRepository = webhookRepository;
        _mpRepository = mpRepository;
    }

    public async Task<MPWebhookEvent> SaveAsync(
        string provider,
        string eventType,
        long? providerPaymentId,
        string? orderId,
        string payload,
        string? headers,
        CancellationToken ct = default
    )
    {
        if (_mpRepository is not null)
        {
            return await _mpRepository.SaveAsync(
                provider,
                eventType,
                providerPaymentId,
                orderId,
                payload,
                headers,
                ct
            );
        }

        var payloadHash = HeaderUtils.ComputeSha256(payload ?? string.Empty);

        var existing = await _webhookRepository.GetByPayloadHashAsync(payloadHash, ct);
        if (existing is not null)
        {
            return existing;
        }

        var ev = new MPWebhookEvent
        {
            Provider = provider,
            EventType = eventType,
            ProviderPaymentId = providerPaymentId,
            OrderId = orderId,
            Payload = payload!,
            PayloadHash = payloadHash,
            Headers = headers,
            SignatureHeader = HeaderUtils.ExtractSignatureFromHeaders(headers),
            ReceivedAt = DateTimeOffset.UtcNow,
            AttemptCount = 1,
        };

        await _webhookRepository.AddAsync(ev, ct);
        return ev;
    }

    public async Task<MPWebhookEvent?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    )
    {
        if (_mpRepository is not null)
            return await _mpRepository.GetByProviderPaymentIdAsync(providerPaymentId, ct);

        return await _webhookRepository.GetByProviderPaymentIdAsync(providerPaymentId, ct);
    }

    public async Task MarkProcessedAsync(
        Guid id,
        bool processed,
        string? result,
        string? orderId = null,
        int? responseStatusCode = null,
        int? processingDurationMs = null,
        CancellationToken ct = default
    )
    {
        if (_mpRepository is not null)
        {
            await _mpRepository.MarkProcessedAsync(
                id,
                processed,
                result,
                orderId,
                responseStatusCode,
                processingDurationMs,
                ct
            );
            return;
        }

        var ev = await _webhookRepository.GetByIdAsync(id, ct);
        if (ev is null)
            return;
        ev.Processed = processed;
        ev.ProcessedAt = DateTimeOffset.UtcNow;
        ev.AttemptCount += 1;
        ev.ProcessingResult = result;
        if (responseStatusCode.HasValue)
            ev.ResponseStatusCode = responseStatusCode.Value;
        if (processingDurationMs.HasValue)
            ev.ProcessingDurationMs = processingDurationMs.Value;
        if (!string.IsNullOrWhiteSpace(orderId))
            ev.OrderId = orderId;
        await _webhookRepository.UpdateAsync(ev, ct);
    }

    public async Task<int> CleanupUnprocessedAsync(int take, CancellationToken ct = default)
    {
        var repo = _mpRepository as IMercadoPagoRepository ?? null;
        var list = repo is not null
            ? await repo.GetUnprocessedAsync(take, ct)
            : await _webhookRepository.GetUnprocessedAsync(take, ct);
        if (list.Count == 0)
            return 0;

        foreach (var ev in list)
        {
            await MarkProcessedAsync(
                ev.Id,
                true,
                "manual_cleanup",
                ev.OrderId,
                ev.ResponseStatusCode,
                ev.ProcessingDurationMs,
                ct
            );
        }

        return list.Count;
    }
}
