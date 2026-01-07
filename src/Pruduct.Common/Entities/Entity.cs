using System.ComponentModel.DataAnnotations.Schema;

namespace Pruduct.Common.Entities;

public class Entity<TKey> : BaseEntity<TKey>
    where TKey : new()
{
    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset UpdatedAt { get; set; }

    protected Entity()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
