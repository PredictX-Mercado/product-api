namespace Product.Api.Configuration;

public static class RolesConfiguration
{
    public static IServiceCollection AddRolePolicies(this IServiceCollection services)
    {
        services
            .AddAuthorizationBuilder()
            .AddPolicy("RequireAdminL1", p => p.RequireRole("ADMIN_L1", "ADMIN_L2", "ADMIN_L3"))
            .AddPolicy("RequireAdminL2", p => p.RequireRole("ADMIN_L2", "ADMIN_L3"))
            .AddPolicy("RequireAdminL3", p => p.RequireRole("ADMIN_L3"));

        return services;
    }
}
