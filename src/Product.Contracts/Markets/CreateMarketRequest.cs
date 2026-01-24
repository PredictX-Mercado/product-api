using System.ComponentModel.DataAnnotations;

namespace Product.Contracts.Markets;

public class CreateMarketRequest
{
    [Required]
    [StringLength(300, MinimumLength = 10)]
    public string Title { get; set; } = null!;

    [Required]
    [StringLength(5000, MinimumLength = 50)]
    public string Description { get; set; } = null!;

    [Required]
    public string Category { get; set; } = null!;

    public List<string>? Tags { get; set; }

    [Required]
    [Range(1, 99)]
    public int Probability { get; set; } = 50;

    [Required]
    public DateTimeOffset ClosingDate { get; set; }

    [Required]
    public DateTimeOffset ResolutionDate { get; set; }

    [Required]
    [StringLength(1000)]
    public string ResolutionSource { get; set; } = null!;

    public bool Featured { get; set; }
}
