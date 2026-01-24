using Product.Common.Entities;
using Product.Data.Models.Markets.Categories;

namespace Product.Data.Models.Markets;

public class MarketCategory : Entity<Guid>
{
    public Guid MarketId { get; set; }
    public int CategoryId { get; set; }

    public Market? Market { get; set; }
    public Category? Category { get; set; }
}
