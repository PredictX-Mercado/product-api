using Product.Common.Entities;

namespace Product.Data.Models.Markets;

public class IdempotencyRecord : Entity<Guid>
{
    public string Key { get; set; } = null!;
    public Guid UserId { get; set; }
    public string ResultPayload { get; set; } = null!;
}
