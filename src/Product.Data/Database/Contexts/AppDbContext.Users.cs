using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Auth;
using Product.Data.Models.Users;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<UserPersonalData> UserPersonalData => Set<UserPersonalData>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
}
