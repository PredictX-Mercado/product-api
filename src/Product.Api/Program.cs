using Product.Api.Configuration;
using Product.Api.Hubs;
using Product.Business.Interfaces.Users;
using Serilog;

DotEnvLoader.LoadIfDevelopment();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console()
);

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// Register MarketHub
app.MapHub<MarketHub>("/hubs/market");

app.UseApiPipeline();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    await seeder.SeedAsync(app.Configuration);
}

app.Run();
