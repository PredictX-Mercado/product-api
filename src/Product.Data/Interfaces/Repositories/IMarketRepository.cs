using Product.Data.Models.Markets;

namespace Product.Data.Interfaces.Repositories;

public interface IMarketRepository
{
    Task<Market?> GetByIdAsync(Guid marketId, CancellationToken ct = default);
    Task UpdateAsync(Market market, CancellationToken ct = default);
    Task AddAsync(Market market, CancellationToken ct = default);
    Task<IdempotencyRecord?> GetIdempotencyRecordAsync(
        string key,
        Guid userId,
        CancellationToken ct = default
    );
    Task CreateMarketWithIdempotencyAsync(
        Market market,
        IdempotencyRecord? idem,
        CancellationToken ct = default
    );
}
