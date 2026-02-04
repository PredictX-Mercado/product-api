using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Markets;

namespace Product.Data.Repositories;

public class RiskTermsRepository(AppDbContext db) : IRiskTermsRepository
{
    public async Task<RiskTerms?> GetByUserMarketVersionAsync(
        Guid userId,
        Guid marketId,
        string termVersion,
        CancellationToken ct = default
    )
    {
        return await db.RiskTerms.FirstOrDefaultAsync(
            x => x.UserId == userId && x.MarketId == marketId && x.TermVersion == termVersion,
            ct
        );
    }

    public async Task<RiskTerms?> GetLatestByUserMarketAsync(
        Guid userId,
        Guid marketId,
        CancellationToken ct = default
    )
    {
        return await db
            .RiskTerms.Where(x => x.UserId == userId && x.MarketId == marketId)
            .OrderByDescending(x => x.AcceptedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RiskTerms?> GetLatestByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await db
            .RiskTerms.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AcceptedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> HasAcceptedAsync(
        Guid userId,
        Guid marketId,
        string termVersion,
        CancellationToken ct = default
    )
    {
        return await db.RiskTerms.AnyAsync(
            x => x.UserId == userId && x.MarketId == marketId && x.TermVersion == termVersion,
            ct
        );
    }

    public async Task AddAsync(RiskTerms acceptance, CancellationToken ct = default)
    {
        db.RiskTerms.Add(acceptance);
        await db.SaveChangesAsync(ct);
    }
}
