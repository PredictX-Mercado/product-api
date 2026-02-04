using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Product.Data.Database.Contexts;

public partial class AppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply Enum -> string conversion for all enum properties across entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (clrType is null)
                continue;

            var enumProps = clrType
                .GetProperties()
                .Where(p =>
                    p.PropertyType.IsEnum
                    || (Nullable.GetUnderlyingType(p.PropertyType)?.IsEnum == true)
                );

            if (!enumProps.Any())
                continue;

            var entityBuilder = modelBuilder.Entity(clrType);

            foreach (var pi in enumProps)
            {
                var enumType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                var converterType = typeof(EnumToStringConverter<>).MakeGenericType(enumType);
                var converter = (ValueConverter?)Activator.CreateInstance(converterType);
                if (converter is null)
                    continue;

                entityBuilder.Property(pi.Name).HasConversion(converter);
            }
        }
    }
}
