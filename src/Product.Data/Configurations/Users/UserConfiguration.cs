using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Product.Data.Configurations.Users;

using Product.Data.Models.Users;

public class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();

        builder.Property(x => x.AvatarUrl).HasMaxLength(512);

        builder.Property(x => x.UserName).HasMaxLength(256);

        builder.Property(x => x.NormalizedUserName).HasMaxLength(256);

        builder.Property(x => x.Email).HasMaxLength(256);

        builder.Property(x => x.NormalizedEmail).HasMaxLength(256);

        builder.Property(x => x.EmailConfirmed).IsRequired();

        builder.Property(x => x.EmailVerifiedAt).HasColumnType("timestamp with time zone");

        builder.Property(x => x.PhoneNumber);

        builder.Property(x => x.PasswordHash);

        builder.Property(x => x.SecurityStamp);

        builder.Property(x => x.ConcurrencyStamp);

        builder.Property(x => x.TwoFactorEnabled).IsRequired();

        builder.Property(x => x.LockoutEnd).HasColumnType("timestamp with time zone");

        builder.Property(x => x.LockoutEnabled).IsRequired();

        builder.Property(x => x.AccessFailedCount).IsRequired();

        builder.Property(x => x.Role).HasMaxLength(32).IsRequired();

        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
    }
}
