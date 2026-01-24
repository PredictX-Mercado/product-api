using Microsoft.AspNetCore.SignalR;

namespace Product.Api.Hubs;

/// <summary>
/// SignalR hub para notificações relacionadas a mercados.
/// Clientes chamam `JoinMarket` para receber atualizações de um mercado específico
/// e `LeaveMarket` para parar de receber.
/// O servidor envia eventos via `IHubContext<MarketHub>`.
/// </summary>
public class MarketHub : Hub
{
    /// <summary>
    /// Adiciona a conexão atual ao grupo do mercado.
    /// Grupo: "market-{marketId}".
    /// </summary>
    public Task JoinMarket(Guid marketId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, $"market-{marketId}");
    }

    /// <summary>
    /// Remove a conexão atual do grupo do mercado.
    /// </summary>
    public Task LeaveMarket(Guid marketId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"market-{marketId}");
    }

    /// <summary>
    /// Opcional: associa a conexão ao identificador do usuário para facilitar envios direcionados do cliente.
    /// Clientes podem chamar isso após autenticação (não obrigatório se o servidor usar User identifiers).
    /// </summary>
    public Task RegisterUser(string userId)
    {
        // adiciona a conexão a um grupo por usuário (opcional)
        return Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }

    public override Task OnConnectedAsync()
    {
        // comportamento customizável: logging, métricas, etc.
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // comportamento customizável: cleanup, logging
        return base.OnDisconnectedAsync(exception);
    }
}
