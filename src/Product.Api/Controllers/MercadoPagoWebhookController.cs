using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Product.Business.Interfaces.Payments;
using Product.Business.Interfaces.Wallet;
using Product.Business.Options;
using Product.Data.Models.Webhooks;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks/mercadopago")]
public class MercadoPagoWebhookController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MercadoPagoOptions _opt;
    private readonly IOrderService _orderService;
    private readonly IWebhookService _webhookService;
    private readonly IWalletService _walletService;

    public MercadoPagoWebhookController(
        IHttpClientFactory httpClientFactory,
        IOptions<MercadoPagoOptions> opt,
        IOrderService orderService,
        IWebhookService webhookService,
        IWalletService walletService
    )
    {
        _httpClientFactory = httpClientFactory;
        _opt = opt.Value;
        _orderService = orderService;
        _webhookService = webhookService;
        _walletService = walletService;
    }

    private string GetAccessToken() => _opt.MP_ACCESS_TOKEN_LIVE ?? string.Empty;

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        var raw = await new StreamReader(Request.Body).ReadToEndAsync();

        long? paymentId = null;
        if (long.TryParse(Request.Query["data.id"], out var q1))
            paymentId = q1;
        else if (long.TryParse(Request.Query["id"], out var q2))
            paymentId = q2;

        if (paymentId is null && !string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (
                    doc.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty("id", out var idEl)
                )
                {
                    if (
                        idEl.ValueKind == JsonValueKind.String
                        && long.TryParse(idEl.GetString(), out var v)
                    )
                        paymentId = v;
                    else if (idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt64(out var v2))
                        paymentId = v2;
                }
            }
            catch { }
        }

        // Save headers for audit (concise)
        var headers = string.Join(
            ";",
            Request.Headers.Select(h => $"{h.Key}:{string.Join(',', h.Value!)}")
        );

        // Deduplicate: if we already processed this providerPaymentId, ignore
        if (paymentId is not null)
        {
            try
            {
                var existing = await _webhook_service_safe_get(paymentId.Value);
                if (existing is not null && existing.Processed)
                    return Ok();
            }
            catch
            { /* ignore db read errors and continue to persist incoming payload */
            }
        }

        // Persist incoming webhook for audit/replay
        MPWebhookEvent? saved = null;
        try
        {
            saved = await _webhookService.SaveAsync(
                "mercadopago",
                "payment",
                paymentId,
                null,
                raw,
                headers
            );
        }
        catch
        { /* swallow persistence errors */
        }

        if (paymentId is null)
            return Ok();

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
            return Ok();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync(
            $"https://api.mercadopago.com/v1/payments/{paymentId.Value}"
        );
        var paymentJson = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            if (saved is not null)
                await _webhookService.MarkProcessedAsync(
                    saved.Id,
                    false,
                    $"MP GET failed: {resp.StatusCode}"
                );
            return Ok();
        }

        using var paymentDoc = JsonDocument.Parse(paymentJson);
        var root = paymentDoc.RootElement;

        var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
        var orderId = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null;

        // Atualizar no DB (idempotente)
        try
        {
            if (!string.IsNullOrWhiteSpace(orderId) && !string.IsNullOrWhiteSpace(status))
            {
                await _orderService.UpdateStatusAsync(orderId!, status!, paymentId);

                // If we have an associated PaymentIntent (orderId is a Guid), and payment approved,
                // confirm deposit so PaymentIntent.ExternalPaymentId is persisted and ledger entry created.
                if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
                {
                    if (Guid.TryParse(orderId, out var intentId))
                    {
                        try
                        {
                            await _walletService.ConfirmDepositAsync(
                                intentId,
                                paymentId?.ToString() ?? string.Empty
                            );
                        }
                        catch
                        { /* swallow wallet errors */
                        }
                    }
                }

                if (saved is not null)
                    await _webhookService.MarkProcessedAsync(
                        saved.Id,
                        true,
                        $"Order {orderId} updated to {status}",
                        orderId
                    );
            }
            else
            {
                if (saved is not null)
                    await _webhookService.MarkProcessedAsync(
                        saved.Id,
                        false,
                        "Missing orderId or status in MP response",
                        orderId
                    );
            }
        }
        catch (Exception ex)
        {
            if (saved is not null)
                await _webhookService.MarkProcessedAsync(saved.Id, false, ex.Message, orderId);
        }

        return Ok();
    }

    // helper: safe DB read wrapper to avoid compile-time reference inside try above
    private async Task<MPWebhookEvent?> _webhook_service_safe_get(long providerPaymentId)
    {
        try
        {
            return await _webhookService.GetByProviderPaymentIdAsync(providerPaymentId);
        }
        catch
        {
            return null;
        }
    }
}
