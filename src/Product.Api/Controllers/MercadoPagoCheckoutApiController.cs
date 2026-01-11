using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Product.Business.Options;
using Product.Business.Interfaces.Payments;
using Product.Contracts.Payments;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/payments/mercadopago")]
public class MercadoPagoCheckoutApiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MercadoPagoOptions _opt;
    private readonly IOrderService _orderService;

    public MercadoPagoCheckoutApiController(
        IHttpClientFactory httpClientFactory,
        IOptions<MercadoPagoOptions> opt,
        IOrderService orderService
    )
    {
        _httpClientFactory = httpClientFactory;
        _opt = opt.Value;
        _orderService = orderService;
    }

    private string GetAccessToken()
    {
        return _opt.MP_ACCESS_TOKEN_TEST
            ?? throw new InvalidOperationException("MP token não configurado.");
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
    public async Task<ActionResult<PixResponse>> CreatePix([FromBody] CreatePixRequest req)
    {
        var client = CreateMpClient();
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.NewGuid().ToString("N"));

        var payload = new
        {
            transaction_amount = req.Amount,
            description = req.Description,
            payment_method_id = "pix",
            payer = new { email = req.BuyerEmail },
            external_reference = req.OrderId,
            notification_url = _opt.MP_WEBHOOK_URL,
        };

        var resp = await client.PostAsJsonAsync("https://api.mercadopago.com/v1/payments", payload);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var paymentId = root.GetProperty("id").GetInt64();
        var status = root.TryGetProperty("status", out var st) ? st.GetString() : "unknown";

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
            await _orderService.CreateOrUpdateAsync(req.OrderId, req.Amount, "BRL", "mercadopago", paymentId, status ?? "pending", "pix");
        }
        catch { /* swallow persistence errors to avoid breaking payment flow */ }

        return Ok(new PixResponse(paymentId, qrBase64, qrCode, expiresAt, status ?? "unknown"));
    }

    [HttpPost("card")]
    public async Task<IActionResult> CreateCard([FromBody] CreateCardRequest req)
    {
        var client = CreateMpClient();
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.NewGuid().ToString("N"));

        var payload = new
        {
            transaction_amount = req.Amount,
            token = req.Token,
            installments = req.Installments <= 0 ? 1 : req.Installments,
            payment_method_id = req.PaymentMethodId,
            description = req.Description,
            payer = new { email = req.BuyerEmail },
            external_reference = req.OrderId,
            notification_url = _opt.MP_WEBHOOK_URL,
        };

        var resp = await client.PostAsJsonAsync("https://api.mercadopago.com/v1/payments", payload);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, body);

        // Atualizar Order com id/status retornado pelo MP
        try
        {
            using var tmp = JsonDocument.Parse(body);
            var root2 = tmp.RootElement;
            var id = root2.GetProperty("id").GetInt64();
            var st = root2.TryGetProperty("status", out var s2) ? s2.GetString() ?? "pending" : "pending";
            await _orderService.CreateOrUpdateAsync(req.OrderId, req.Amount, "BRL", "mercadopago", id, st, "card");
        }
        catch { }

        return Content(body, "application/json");
    }

    [HttpPost("boleto")]
    public async Task<IActionResult> CreateBoleto([FromBody] CreateBoletoRequest req)
    {
        var client = CreateMpClient();
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.NewGuid().ToString("N"));

        var payload = new
        {
            transaction_amount = req.Amount,
            description = req.Description,
            payment_method_id = "bolbradesco",
            payer = new
            {
                email = req.BuyerEmail,
                first_name = req.FirstName,
                last_name = req.LastName,
                identification = new
                {
                    type = req.IdentificationType,
                    number = req.IdentificationNumber,
                },
            },
            external_reference = req.OrderId,
            notification_url = _opt.MP_WEBHOOK_URL,
        };

        var resp = await client.PostAsJsonAsync("https://api.mercadopago.com/v1/payments", payload);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, body);

        // Atualizar Order com id/status retornado pelo MP (boleto)
        try
        {
            using var tmp = JsonDocument.Parse(body);
            var root2 = tmp.RootElement;
            var id = root2.GetProperty("id").GetInt64();
            var st = root2.TryGetProperty("status", out var s2) ? s2.GetString() ?? "pending" : "pending";
            await _orderService.CreateOrUpdateAsync(req.OrderId, req.Amount, "BRL", "mercadopago", id, st, "boleto");
        }
        catch { }

        return Content(body, "application/json");
    }

    [HttpGet("status/{paymentId:long}")]
    public async Task<ActionResult<PaymentStatusResponse>> GetPaymentStatus(
        [FromRoute] long paymentId
    )
    {
        var client = CreateMpClient();

        var resp = await client.GetAsync($"https://api.mercadopago.com/v1/payments/{paymentId}");
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var st) ? st.GetString() : "unknown";
        var statusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null;
        var ext = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null;
        decimal? amount = null;
        if (root.TryGetProperty("transaction_amount", out var ta) && ta.TryGetDecimal(out var a))
            amount = a;

        return Ok(
            new PaymentStatusResponse(paymentId, status ?? "unknown", statusDetail, ext, amount)
        );
    }
}
