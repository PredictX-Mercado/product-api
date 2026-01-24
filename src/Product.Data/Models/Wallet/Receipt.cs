using Product.Common.Entities;

namespace Product.Data.Models.Wallet;

public class Receipt : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty; // deposit|withdraw|buy
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string? Provider { get; set; }
    public long? ProviderPaymentId { get; set; }
    public string? ProviderPaymentIdText { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? PayloadJson { get; set; }
}
