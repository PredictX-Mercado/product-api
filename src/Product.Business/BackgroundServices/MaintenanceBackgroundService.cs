using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Product.Business.Interfaces.Payments;
using Product.Business.Interfaces.Wallet;
using Product.Business.Options;

namespace Product.Business.BackgroundServices;

/// <summary>
/// Periodically cleans up unprocessed webhooks and backfills missing receipts
/// so the tables don't accumulate orphaned rows.
/// </summary>
public class MaintenanceBackgroundService(
    IServiceProvider services,
    IOptions<MaintenanceOptions> options,
    ILogger<MaintenanceBackgroundService> logger
) : BackgroundService
{
    private readonly IServiceProvider _services = services;
    private readonly MaintenanceOptions _options = options.Value;
    private readonly ILogger<MaintenanceBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // simple two timers using a single loop and counters
        var webhookInterval = TimeSpan.FromSeconds(
            Math.Max(30, _options.WebhookCleanupIntervalSeconds)
        );
        var receiptInterval = TimeSpan.FromSeconds(
            Math.Max(30, _options.ReceiptBackfillIntervalSeconds)
        );
        var webhookNext = DateTimeOffset.UtcNow; // run immediately
        var receiptNext = DateTimeOffset.UtcNow; // run immediately

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            if (now >= webhookNext)
            {
                await RunWebhookCleanupAsync(stoppingToken);
                webhookNext = now.Add(webhookInterval);
            }

            if (now >= receiptNext)
            {
                await RunReceiptBackfillAsync(stoppingToken);
                receiptNext = now.Add(receiptInterval);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task RunWebhookCleanupAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IWebhookService>();
            var cleaned = await svc.CleanupUnprocessedAsync(_options.BatchSize, ct);
            _logger.LogInformation("Maintenance: webhook cleanup ran, cleaned={Count}", cleaned);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Maintenance: webhook cleanup failed");
        }
    }

    private async Task RunReceiptBackfillAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var receipts = scope.ServiceProvider.GetRequiredService<IReceiptService>();

            var createdDeposits = await receipts.BackfillDepositReceiptsAsync(
                _options.BatchSize,
                ct
            );
            var createdBuys = await receipts.BackfillBuyReceiptsAsync(_options.BatchSize, ct);

            if (createdDeposits + createdBuys > 0)
            {
                _logger.LogInformation(
                    "Maintenance: backfilled receipts (deposits={Deposits}, buys={Buys})",
                    createdDeposits,
                    createdBuys
                );
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Maintenance: receipt backfill failed");
        }
    }
}
