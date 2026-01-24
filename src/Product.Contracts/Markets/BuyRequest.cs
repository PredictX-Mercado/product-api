using System.ComponentModel.DataAnnotations;

namespace Product.Contracts.Markets;

public class BuyRequest
{
    [Required]
    public Guid MarketId { get; set; }

    [Required]
    [RegularExpression("^(yes|no)$", ErrorMessage = "Side must be 'yes' or 'no'.")]
    public string Side { get; set; } = null!;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public string? IdempotencyKey { get; set; }
}
