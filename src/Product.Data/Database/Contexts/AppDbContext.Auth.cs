using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Auth;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
}
