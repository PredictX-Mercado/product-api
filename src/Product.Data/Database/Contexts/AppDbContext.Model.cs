using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Users;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map identity entities to canonical AspNet* tables without changing model classes
        modelBuilder.Entity<User>().ToTable("AspNetUsers");
        modelBuilder.Entity<Role>().ToTable("AspNetRoles");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
