using MarketEntity = Product.Data.Models.Markets.Market;

namespace Product.Business.Interfaces.Categories;

public interface ICategoryService
{
    Task EnsureDefaultCategoriesAsync(CancellationToken ct = default);
    Task AssociateCategoryWithMarketAsync(MarketEntity market, CancellationToken ct = default);
}
