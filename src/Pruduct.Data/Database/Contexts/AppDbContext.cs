using Microsoft.EntityFrameworkCore;

namespace Pruduct.Data.Database.Contexts;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
}
