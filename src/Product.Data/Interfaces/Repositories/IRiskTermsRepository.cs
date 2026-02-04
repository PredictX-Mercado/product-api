using Product.Data.Models.Markets;

namespace Product.Data.Interfaces.Repositories;

public interface IRiskTermsRepository
{
    Task<RiskTerms?> GetByUserMarketVersionAsync(
        Guid userId,
        Guid marketId,
        string termVersion,
        CancellationToken ct = default
    );
    Task<RiskTerms?> GetLatestByUserMarketAsync(
        Guid userId,
        Guid marketId,
        CancellationToken ct = default
    );
    Task<RiskTerms?> GetLatestByUserAsync(
        Guid userId,
        CancellationToken ct = default
    );
    Task<bool> HasAcceptedAsync(
        Guid userId,
        Guid marketId,
        string termVersion,
        CancellationToken ct = default
    );
    Task AddAsync(RiskTerms acceptance, CancellationToken ct = default);
}
