namespace Product.Business.Options;

public class MaintenanceOptions
{
    /// <summary>
    /// Interval (seconds) between webhook cleanup runs.
    /// </summary>
    public int WebhookCleanupIntervalSeconds { get; set; } = 120;

    /// <summary>
    /// Interval (seconds) between receipt backfill runs.
    /// </summary>
    public int ReceiptBackfillIntervalSeconds { get; set; } = 180;

    /// <summary>
    /// Maximum items to process per batch.
    /// </summary>
    public int BatchSize { get; set; } = 200;
}
