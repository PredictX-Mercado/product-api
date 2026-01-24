using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Product.Common.Entities;

namespace Product.Data.Models.Users;

public class ApplicationUser : AuditedUserEntity<Guid>, IBaseEntity<Guid>
{
    public string Name { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public override string? UserName { get; set; }
    public override string? NormalizedUserName { get; set; }

    [NotMapped]
    public List<string> Role { get; set; } = new List<string>();

    [Column("Role")]
    public string? RoleRaw { get; set; }
    public override string? Email { get; set; }
    public override string? NormalizedEmail { get; set; }
    public override bool EmailConfirmed { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public override string? PhoneNumber { get; set; }
    public override string? PasswordHash { get; set; }
    public override string? SecurityStamp { get; set; }
    public override string? ConcurrencyStamp { get; set; }
    public override bool TwoFactorEnabled { get; set; }
    public override DateTimeOffset? LockoutEnd { get; set; }
    public override bool LockoutEnabled { get; set; }
    public override int AccessFailedCount { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public UserPersonalData? PersonalData { get; set; }
}
