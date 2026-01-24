using Microsoft.AspNetCore.SignalR;
using Product.Api.Hubs;
using Product.Business.Interfaces.Notifications;

namespace Product.Api.Services;

public class MarketNotifier : IMarketNotifier
{
    private readonly IHubContext<MarketHub> _hub;

    public MarketNotifier(IHubContext<MarketHub> hub)
    {
        _hub = hub;
    }

    public async Task NotifyMarketUpdated(
        Guid marketId,
        object payload,
        CancellationToken ct = default
    )
    {
        await _hub.Clients.Group($"market-{marketId}").SendAsync("MarketUpdated", payload, ct);
    }

    public async Task NotifyUserBalanceUpdated(
        Guid userId,
        decimal newBalance,
        CancellationToken ct = default
    )
    {
        await _hub
            .Clients.User(userId.ToString())
            .SendAsync("BalanceUpdated", new { newBalance }, ct);
    }
}
