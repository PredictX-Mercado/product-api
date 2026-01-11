using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;

namespace Product.Data.Database.Factories;

public class AppDbContextFactory : DbContextFactory<AppDbContext>
{
    protected override AppDbContext CreateDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
