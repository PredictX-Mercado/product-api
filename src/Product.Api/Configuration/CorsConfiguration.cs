namespace Product.Api.Configuration;

public static class CorsConfiguration
{
    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var configured =
            configuration.GetSection("Cors:Allow").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(o =>
        {
            o.AddPolicy(
                "Allowlist",
                p => p.WithOrigins(configured).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
            );
        });

        return services;
    }
}
