using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Markets;
using Product.Data.Models.Markets.Categories;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<Market> Markets { get; set; } = null!;
    public DbSet<Position> Positions { get; set; } = null!;
    public DbSet<Transaction> MarketTransactions { get; set; } = null!;
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; } = null!;
    public DbSet<RiskTerms> RiskTerms { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<MarketCategory> MarketCategories { get; set; } = null!;
}
