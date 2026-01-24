using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Data.Models.Markets;

namespace Product.Data.Configurations.Markets;

public class MarketConfiguration : IEntityTypeConfiguration<Market>
{
    public void Configure(EntityTypeBuilder<Market> builder)
    {
        builder.ToTable("Markets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.YesPrice).HasPrecision(18, 6);
        builder.Property(x => x.NoPrice).HasPrecision(18, 6);
        builder.Property(x => x.VolumeTotal).HasPrecision(18, 6);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
