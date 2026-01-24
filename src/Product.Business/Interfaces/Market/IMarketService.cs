using Product.Contracts.Markets;

namespace Product.Business.Interfaces.Market;

public interface IMarketService
{
    Task<MarketResponse?> GetMarketAsync(
        Guid marketId,
        Guid? userId = null,
        CancellationToken ct = default
    );
    Task<(
        IEnumerable<MarketResponse> Items,
        int Total,
        int Page,
        int PageSize
    )> ExploreMarketsAsync(ExploreFilterRequest req, CancellationToken ct = default);
    Task<MarketResponse> CreateMarketAsync(
        CreateMarketRequest req,
        Guid? userId,
        string? userEmail,
        bool isAdminL2,
        string? idempotencyKey,
        bool confirmLowLiquidity,
        CancellationToken ct = default
    );
    Task DeleteMarketAsync(Guid marketId, Guid? userId, CancellationToken ct = default);
    Task<BuyResponse> BuyAsync(
        Guid marketId,
        Guid userId,
        string side,
        decimal amount,
        string? idempotencyKey = null,
        CancellationToken ct = default
    );

    Task<IEnumerable<MarketHistoryPoint>> GetMarketHistoryAsync(
        Guid marketId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? resolution = null,
        CancellationToken ct = default
    );
}
