using Microsoft.Extensions.Logging;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Audit;

namespace Product.Data.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditRepository> _logger;

    public AuditRepository(AppDbContext db, ILogger<AuditRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
    {
        try
        {
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Audit saved: Action={Action} Entity={Entity} EntityId={EntityId} Id={Id}",
                log.Action,
                log.Entity,
                log.EntityId,
                log.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save audit log: Action={Action} Entity={Entity} EntityId={EntityId}",
                log.Action,
                log.Entity,
                log.EntityId
            );
            throw;
        }
    }
}
