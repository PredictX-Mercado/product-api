using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Data.Models.Markets;

namespace Product.Data.Configurations.Markets;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Side).IsRequired();
        builder.Property(x => x.AveragePrice).HasPrecision(18, 6);
        builder.Property(x => x.TotalInvested).HasPrecision(18, 6);
        builder.HasIndex(x => new
        {
            x.UserId,
            x.MarketId,
            x.Side,
        });
    }
}
