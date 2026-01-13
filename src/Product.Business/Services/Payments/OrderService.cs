using Microsoft.EntityFrameworkCore;
using Product.Business.Interfaces.Payments;
using Product.Data.Database.Contexts;
using Product.Data.Models.Orders;

namespace Product.Business.Services.Payments;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Order?> GetByExternalIdAsync(
        string externalOrderId,
        CancellationToken ct = default
    )
    {
        return await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == externalOrderId, ct);
    }

    public async Task<Order> CreateOrUpdateAsync(
        string externalOrderId,
        decimal amount,
        string currency,
        string provider,
        long? providerPaymentId,
        string status,
        string? statusDetail,
        string paymentMethod,
        CancellationToken ct = default
    )
    {
        var existing = await GetByExternalIdAsync(externalOrderId, ct);
        if (existing is null)
        {
            var ord = new Order
            {
                OrderId = externalOrderId,
                Amount = amount,
                Currency = currency,
                Provider = provider,
                ProviderPaymentId = providerPaymentId,
                Status = status,
                StatusDetail = statusDetail,
                PaymentMethod = paymentMethod,
            };
            _db.Orders.Add(ord);
            await _db.SaveChangesAsync(ct);
            return ord;
        }

        // Idempotent update: if already approved, avoid downgrading
        if (existing.Status == "approved")
            return existing;

        existing.Amount = amount;
        existing.Currency = currency;
        existing.Provider = provider;
        existing.ProviderPaymentId = providerPaymentId ?? existing.ProviderPaymentId;
        existing.Status = status;
        existing.StatusDetail = statusDetail ?? existing.StatusDetail;
        existing.PaymentMethod = paymentMethod;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<Order?> UpdateStatusAsync(
        string externalOrderId,
        string status,
        string? statusDetail,
        long? providerPaymentId,
        CancellationToken ct = default
    )
    {
        var existing = await GetByExternalIdAsync(externalOrderId, ct);
        if (existing is null)
            return null;

        if (existing.Status == "approved")
            return existing;

        existing.Status = status;
        existing.StatusDetail = statusDetail ?? existing.StatusDetail;
        if (providerPaymentId is not null)
            existing.ProviderPaymentId = providerPaymentId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<Order?> UpdateStatusByProviderIdAsync(
        long providerPaymentId,
        string status,
        string? statusDetail,
        CancellationToken ct = default
    )
    {
        var existing = await _db.Orders.FirstOrDefaultAsync(
            o => o.ProviderPaymentId == providerPaymentId,
            ct
        );
        if (existing is null)
            return null;

        if (existing.Status == "approved")
            return existing;

        existing.Status = status;
        existing.StatusDetail = statusDetail ?? existing.StatusDetail;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return existing;
    }
}
