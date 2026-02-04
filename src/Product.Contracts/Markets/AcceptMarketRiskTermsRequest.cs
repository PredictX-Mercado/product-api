using System.ComponentModel.DataAnnotations;

namespace Product.Contracts.Markets;

public class AcceptMarketRiskTermsRequest
{
    [Required]
    public Guid MarketId { get; set; }

    [Required]
    public string TermVersion { get; set; } = string.Empty;

    public string? TermSnapshot { get; set; }
    public string? TermHash { get; set; }

    // preenchido no backend com base no usuario autenticado; opcional para o cliente
    public string? Username { get; set; }
}
