using Product.Common.Entities;
using Product.Data.Models.Users;

namespace Product.Data.Models.Auth;

public class EmailVerificationToken : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public User? User { get; set; }
}
