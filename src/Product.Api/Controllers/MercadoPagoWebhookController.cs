using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Product.Business.Interfaces.Payments;
using Product.Business.Options;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/webhooks/mercadopago")]
public class MercadoPagoWebhookController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MercadoPagoOptions _opt;
    private readonly IOrderService _orderService;

    public MercadoPagoWebhookController(
        IHttpClientFactory httpClientFactory,
        IOptions<MercadoPagoOptions> opt,
        IOrderService orderService
    )
    {
        _httpClientFactory = httpClientFactory;
        _opt = opt.Value;
        _orderService = orderService;
    }

    private string GetAccessToken() => _opt.MP_ACCESS_TOKEN_TEST ?? string.Empty;

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
            return Ok();

        using var paymentDoc = JsonDocument.Parse(paymentJson);
        var root = paymentDoc.RootElement;

        var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
        var orderId = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null;

        // Atualizar no DB (idempotente)
        try
        {
            if (!string.IsNullOrWhiteSpace(orderId) && !string.IsNullOrWhiteSpace(status))
            {
                // providerPaymentId is paymentId
                await _orderService.UpdateStatusAsync(orderId!, status!, paymentId);
            }
        }
        catch { }

        return Ok();
    }
}
