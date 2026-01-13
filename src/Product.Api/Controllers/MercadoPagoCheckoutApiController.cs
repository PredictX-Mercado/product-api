using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Product.Business.Interfaces.Payments;
using Product.Business.Options;
using Product.Contracts.Payments;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/payments/mercadopago")]
public class MercadoPagoCheckoutApiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MercadoPagoOptions _opt;
    private readonly IOrderService _orderService;
    private readonly ILogger<MercadoPagoCheckoutApiController> _logger;

    public MercadoPagoCheckoutApiController(
        IHttpClientFactory httpClientFactory,
        IOptions<MercadoPagoOptions> opt,
        IOrderService orderService,
        ILogger<MercadoPagoCheckoutApiController> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _opt = opt.Value;
        _orderService = orderService;
        _logger = logger;
    }

    private IActionResult BuildMpErrorResponse(HttpResponseMessage resp, string body)
    {
        string? statusDetail = null;
        string? rejectionReason = null;

        try
        {
            using var doc = JsonDocument.Parse(body ?? string.Empty);
            var root = doc.RootElement;
            if (
                root.TryGetProperty("status_detail", out var sd)
                && sd.ValueKind == JsonValueKind.String
            )
                statusDetail = sd.GetString();
            if (
                root.TryGetProperty("rejection_reason", out var rr)
                && rr.ValueKind == JsonValueKind.String
            )
                rejectionReason = rr.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao parsear body de erro do Mercado Pago");
        }

        _logger.LogInformation(
            "MercadoPago returned {StatusCode} (status_detail={StatusDetail}, rejection_reason={RejectionReason}) body={Body}",
            (int)resp.StatusCode,
            statusDetail,
            rejectionReason,
            body
        );

        return StatusCode(
            (int)resp.StatusCode,
            new
            {
                Error = "MercadoPagoError",
                StatusCode = (int)resp.StatusCode,
                StatusDetail = statusDetail,
                RejectionReason = rejectionReason,
                Raw = body,
            }
        );
    }

    private static string NormalizeMpStatus(string? mpStatus)
    {
        if (string.IsNullOrWhiteSpace(mpStatus))
            return "unknown";

        var s = mpStatus.Trim().ToLowerInvariant();

        // Map common MercadoPago statuses to simplified set used by frontend
        return s switch
        {
            "approved" => "approved",
            "authorized" => "approved",
            "paid" => "approved",
            "pending" => "pending",
            "in_process" => "pending",
            "rejected" => "rejected",
            "refunded" => "rejected",
            "cancelled" => "rejected",
            "cancelled_by_user" => "rejected",
            _ => mpStatus,
        };
    }

    private string GetAccessToken()
    {
        var live = _opt.MP_ACCESS_TOKEN_LIVE;
        if (!string.IsNullOrWhiteSpace(live))
            return live;

        var test = _opt.MP_ACCESS_TOKEN_TEST;
        if (!string.IsNullOrWhiteSpace(test))
            return test;

        throw new InvalidOperationException("MP token não configurado (LIVE ou TEST).");
    }

    private HttpClient CreateMpClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GetAccessToken()
        );
        return client;
    }

    [HttpPost("pix")]
    public async Task<IActionResult> CreatePix([FromBody] CreatePixRequest req)
    {
        var client = CreateMpClient();

        var deviceId = Request.Headers["X-meli-session-id"].ToString();

        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.NewGuid().ToString("N"));

        var expires = DateTimeOffset
            .UtcNow.AddMinutes(15)
            .ToOffset(TimeSpan.FromHours(-3))
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz");

        var payload = new Dictionary<string, object?>
        {
            ["transaction_amount"] = req.Amount,
            ["description"] = req.Description,
            ["payment_method_id"] = "pix",
            ["payer"] = new { email = req.BuyerEmail },
            ["external_reference"] = req.OrderId,
            ["date_of_expiration"] = expires,
            ["notification_url"] = _opt.MP_WEBHOOK_URL,
        };

        var httpReq = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.mercadopago.com/v1/payments"
        )
        {
            Content = JsonContent.Create(payload),
        };
        if (!string.IsNullOrWhiteSpace(deviceId))
            httpReq.Headers.TryAddWithoutValidation("X-meli-session-id", deviceId);

        var resp = await client.SendAsync(httpReq);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return BuildMpErrorResponse(resp, body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var paymentId = root.GetProperty("id").GetInt64();
        var status = root.TryGetProperty("status", out var st) ? st.GetString() : "unknown";
        var statusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null;

        string? qrBase64 = null;
        string? qrCode = null;
        DateTimeOffset? expiresAt = null;

        if (
            root.TryGetProperty("point_of_interaction", out var poi)
            && poi.TryGetProperty("transaction_data", out var td)
        )
        {
            if (td.TryGetProperty("qr_code_base64", out var b64))
                qrBase64 = b64.GetString();
            if (td.TryGetProperty("qr_code", out var qrc))
                qrCode = qrc.GetString();
        }

        if (
            root.TryGetProperty("date_of_expiration", out var exp)
            && exp.ValueKind == JsonValueKind.String
        )
            if (DateTimeOffset.TryParse(exp.GetString(), out var dt))
                expiresAt = dt;

        // Persistir Order no DB (status=pending, ProviderPaymentId=paymentId, method=pix)
        try
        {
            var normalizedStatus = NormalizeMpStatus(status);
            await _orderService.CreateOrUpdateAsync(
                req.OrderId,
                req.Amount,
                "BRL",
                "mercadopago",
                paymentId,
                normalizedStatus ?? "pending",
                statusDetail,
                "pix"
            );
        }
        catch
        { /* swallow persistence errors to avoid breaking payment flow */
        }

        return Ok(
            new PixResponse
            {
                PaymentId = paymentId,
                QrCodeBase64 = qrBase64,
                QrCode = qrCode,
                ExpiresAt = expiresAt,
                Status = status ?? "unknown",
            }
        );
    }

    [HttpGet("status/{paymentId:long}")]
    public async Task<IActionResult> GetPaymentStatus([FromRoute] long paymentId)
    {
        var client = CreateMpClient();

        var resp = await client.GetAsync($"https://api.mercadopago.com/v1/payments/{paymentId}");
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return BuildMpErrorResponse(resp, body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var st) ? st.GetString() : "unknown";
        var statusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null;
        var ext = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null;
        decimal? amount = null;
        if (root.TryGetProperty("transaction_amount", out var ta) && ta.TryGetDecimal(out var a))
            amount = a;

        DateTimeOffset? expiresAt = null;
        if (
            root.TryGetProperty("date_of_expiration", out var exp)
            && exp.ValueKind == JsonValueKind.String
        )
            if (DateTimeOffset.TryParse(exp.GetString(), out var dt))
                expiresAt = dt;

        // Normalize MercadoPago status and handle expiration
        var normalized = NormalizeMpStatus(status);

        // If payment is still pending but already past the declared expiration,
        // treat it as expired locally and update order status so front stops polling.
        if (
            string.Equals(normalized, "pending", StringComparison.OrdinalIgnoreCase)
            && expiresAt.HasValue
            && expiresAt.Value <= DateTimeOffset.UtcNow
        )
        {
            normalized = "expired";
        }

        // If normalized status is final, update order status in our system
        var isFinal =
            string.Equals(normalized, "approved", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "expired", StringComparison.OrdinalIgnoreCase);

        if (isFinal)
        {
            if (!string.IsNullOrWhiteSpace(ext))
            {
                try
                {
                    await _orderService.CreateOrUpdateAsync(
                        ext,
                        amount ?? 0m,
                        "BRL",
                        "mercadopago",
                        paymentId,
                        normalized,
                        statusDetail,
                        "pix"
                    );
                }
                catch { }
            }
            else if (paymentId != 0)
            {
                try
                {
                    await _orderService.UpdateStatusByProviderIdAsync(
                        paymentId,
                        normalized,
                        statusDetail
                    );
                }
                catch { }
            }
        }

        return Ok(
            new PaymentStatusResponse
            {
                PaymentId = paymentId,
                Status = normalized ?? "unknown",
                StatusDetail = statusDetail,
                ExternalReference = ext,
                Amount = amount,
                ExpiresAt = expiresAt,
            }
        );
    }
}
