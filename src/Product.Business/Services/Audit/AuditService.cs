using System.Text.Json;
using Product.Business.Interfaces.Audit;
using Product.Data.Database.Contexts;
using Product.Data.Models.Audit;

namespace Product.Business.Services.Audit;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string entity,
        Guid? entityId,
        object? meta = null,
        string? ip = null,
        string? userAgent = null,
        CancellationToken ct = default
    )
    {
        var metaJson = meta is null ? null : JsonSerializer.Serialize(meta);

        _db.AuditLogs.Add(
            new AuditLog
            {
                UserId = userId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                MetaJson = metaJson,
                Ip = ip,
                UserAgent = userAgent,
            }
        );

        await _db.SaveChangesAsync(ct);
    }
}
