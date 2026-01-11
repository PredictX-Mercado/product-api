using System;
using System.IO;
using System.Linq;
using Product.Api.Configuration;
using Product.Business.Interfaces.Users;
using Serilog;

// Carrega arquivo .env automaticamente em ambiente Development (convenience)
var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
if (string.Equals(currentEnv, "Development", StringComparison.OrdinalIgnoreCase))
{
    var candidates = new[]
    {
        ".env",
        Path.Combine("..", ".env"),
        Path.Combine("..", "..", ".env"),
        Path.Combine("..", "..", "..", ".env"),
    };
    var envPath = candidates.FirstOrDefault(File.Exists);
    if (!string.IsNullOrEmpty(envPath))
    {
        foreach (var raw in File.ReadAllLines(envPath))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;
            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;
            var key = line.Substring(0, idx).Trim();
            var val = line.Substring(idx + 1).Trim();
            if (
                (val.StartsWith("\"") && val.EndsWith("\""))
                || (val.StartsWith("'") && val.EndsWith("'"))
            )
                val = val.Substring(1, val.Length - 2);
            Environment.SetEnvironmentVariable(key, val, EnvironmentVariableTarget.Process);
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console()
);

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    await seeder.SeedAsync(app.Configuration);
}

// Auth endpoints are provided by AuthController now (MVC controllers are registered in AddApiServices)

app.Run();
