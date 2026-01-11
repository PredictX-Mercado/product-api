using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Audit;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}
