using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Webhooks;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<MPWebhookEvent> MPWebhookEvent => Set<MPWebhookEvent>();
}
