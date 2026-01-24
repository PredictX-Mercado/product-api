namespace Product.Business.Options;

public class MercadoPagoOptions
{
    // Environment variable style names are kept intentionally
    public string? MP_ACCESS_TOKEN_TEST { get; set; }
    public string? MP_ACCESS_TOKEN_LIVE { get; set; }
    public string? MP_WEBHOOK_URL { get; set; }
    public string? MP_WEBHOOK_SECRET { get; set; }
    public string? APP_FRONT_URL { get; set; }
}
