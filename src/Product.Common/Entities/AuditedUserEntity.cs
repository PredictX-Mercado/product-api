using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Product.Common.Entities;

public class AuditedUserEntity<TKey> : IdentityUser<TKey>, IBaseEntity<TKey>
    where TKey : IEquatable<TKey>, new()
{
    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset UpdatedAt { get; set; }

    public AuditedUserEntity()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
