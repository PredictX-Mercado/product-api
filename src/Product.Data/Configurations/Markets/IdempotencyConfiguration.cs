using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Data.Models.Markets;

namespace Product.Data.Configurations.Markets;

public class IdempotencyConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired();
        builder.Property(x => x.ResultPayload).IsRequired();
        builder.HasIndex(x => new { x.Key, x.UserId }).IsUnique();
    }
}
