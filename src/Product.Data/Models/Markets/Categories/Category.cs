using Product.Common.Entities;

namespace Product.Data.Models.Markets.Categories;

public class Category : Entity<int>
{
    public string Name { get; set; } = null!;
    public string? Slug { get; set; }
}
