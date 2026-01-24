using System.ComponentModel.DataAnnotations.Schema;
using Product.Common.Entities;

namespace Product.Data.Models.Markets;

public class Market : Entity<Guid>
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Category { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? ClosingDate { get; set; }
    public string Status { get; set; } = "open";
    public decimal YesPrice { get; set; }
    public decimal NoPrice { get; set; }
    public decimal VolumeTotal { get; set; }
    public int YesContracts { get; set; }
    public int NoContracts { get; set; }
    public decimal Volume24h { get; set; }
    public decimal Volatility24h { get; set; }

    public byte[]? RowVersion { get; set; }

    public string? Tags { get; set; }
    public bool Featured { get; set; }

    public Guid? CreatedBy { get; set; }
    public string? CreatorEmail { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? ResolutionDate { get; set; }
    public string? ResolutionSource { get; set; }

    public bool LowLiquidityWarning { get; set; }
    public string? ProbabilityBucket { get; set; }
    public string? SearchSnippet { get; set; }
}
