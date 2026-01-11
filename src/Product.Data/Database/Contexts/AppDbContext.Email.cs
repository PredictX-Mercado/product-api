using Microsoft.EntityFrameworkCore;
using Product.Data.Models.Emails;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    public DbSet<QueuedEmail> QueuedEmails => Set<QueuedEmail>();
}
