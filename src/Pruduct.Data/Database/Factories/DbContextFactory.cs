using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Pruduct.Data.Database.Factories;

/// <summary>
/// Base factory to create DbContext instances at design time (migrations).
/// Subclasses provide provider-specific configuration.
/// </summary>
public abstract class DbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext(string[] args)
    {
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Resolve appsettings from the API project when running design-time tools from the data project.
        var basePath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Pruduct.Api")
        );

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionStringName = GetConnectionStringName();
        var connectionString =
            config.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{connectionStringName}' not found."
            );

        return CreateDbContext(connectionString);
    }

    protected virtual string GetConnectionStringName() => "DefaultConnection";

    protected abstract TContext CreateDbContext(string connectionString);
}
