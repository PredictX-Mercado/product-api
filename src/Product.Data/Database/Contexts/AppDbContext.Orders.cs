using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Orders;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<Order> Orders => Set<Order>();
}
