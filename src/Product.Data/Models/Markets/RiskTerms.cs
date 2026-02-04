using System.ComponentModel.DataAnnotations.Schema;
using Product.Common.Entities;

namespace Product.Data.Models.Markets;

public class RiskTerms : Entity<Guid>
{
    public Guid UserId { get; set; }
    public Guid MarketId { get; set; }

    public string TermVersion { get; set; } = string.Empty;
    public string? TermSnapshot { get; set; }
    public string? TermHash { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;
}
