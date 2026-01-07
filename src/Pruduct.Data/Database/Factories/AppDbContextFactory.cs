using Microsoft.EntityFrameworkCore;
using Pruduct.Data.Database.Contexts;

namespace Pruduct.Data.Database.Factories;

public class AppDbContextFactory : DbContextFactory<AppDbContext>
{
    protected override AppDbContext CreateDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
