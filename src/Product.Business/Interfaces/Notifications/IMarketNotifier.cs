namespace Product.Business.Interfaces.Notifications;

public interface IMarketNotifier
{
    Task NotifyMarketUpdated(Guid marketId, object payload, CancellationToken ct = default);
    Task NotifyUserBalanceUpdated(Guid userId, decimal newBalance, CancellationToken ct = default);
}
