using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Markets;

namespace Product.Data.Repositories;

public class MarketRepository(AppDbContext db) : IMarketRepository
{
    public async Task<Market?> GetByIdAsync(Guid marketId, CancellationToken ct = default)
    {
        return await db.Markets.FirstOrDefaultAsync(m => m.Id == marketId, ct);
    }

    public async Task UpdateAsync(Market market, CancellationToken ct = default)
    {
        db.Markets.Update(market);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddAsync(Market market, CancellationToken ct = default)
    {
        db.Markets.Add(market);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IdempotencyRecord?> GetIdempotencyRecordAsync(
        string key,
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await db.IdempotencyRecords.FirstOrDefaultAsync(
            r => r.Key == key && r.UserId == userId,
            ct
        );
    }

    public async Task CreateMarketWithIdempotencyAsync(
        Market market,
        IdempotencyRecord? idem,
        CancellationToken ct = default
    )
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.Markets.Add(market);
            if (idem != null)
            {
                db.IdempotencyRecords.Add(idem);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
