using Product.Data.Models.Orders;

namespace Product.Business.Interfaces.Payments;

public interface IOrderService
{
    Task<Order?> GetByExternalIdAsync(string externalOrderId, CancellationToken ct = default);

    Task<Order> CreateOrUpdateAsync(
        string externalOrderId,
        decimal amount,
        string currency,
        string provider,
        long? providerPaymentId,
        string status,
        string paymentMethod,
        CancellationToken ct = default
    );

    Task<Order?> UpdateStatusAsync(string externalOrderId, string status, long? providerPaymentId, CancellationToken ct = default);
}
