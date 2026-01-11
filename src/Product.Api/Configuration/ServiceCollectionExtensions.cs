using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Product.Business.BackgroundServices;
using Product.Business.Interfaces.Audit;
using Product.Business.Interfaces.Auth;
using Product.Business.Interfaces.Email;
using Product.Business.Interfaces.Payments;
using Product.Business.Interfaces.Users;
using Product.Business.Interfaces.Wallet;
using Product.Business.Options;
using Product.Business.Providers;
using Product.Business.Services.Audit;
using Product.Business.Services.Auth;
using Product.Business.Services.Mailers;
using Product.Business.Services.Payments;
using Product.Business.Services.Users;
using Product.Business.Services.Wallet;
using Product.Business.Validators.Auth;
using Product.Data.Database.Contexts;
using Product.Data.Models.Users;
using AuthEmail = Product.Business.Interfaces.Auth;
using Mailers = Product.Business.Services.Mailers;
using ProblemDetailsExtensions = Hellang.Middleware.ProblemDetails.ProblemDetailsExtensions;

namespace Product.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddControllers().AddNewtonsoftJson();
        services.AddHttpContextAccessor();
        // HttpClientFactory para integrações externas (MercadoPago, etc.)
        services.AddHttpClient();

        // Validation
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssembly(typeof(SignupRequestValidator).Assembly);

        // Options
        services.Configure<IdentityTokenOptions>(
            configuration.GetSection(IdentityTokenOptions.SectionName)
        );
        services.Configure<EmailOptions>(configuration.GetSection("Email")); // <- garante que bate com appsettings "Email"
        services.Configure<GoogleAuthOptions>(
            configuration.GetSection(GoogleAuthOptions.SectionName)
        );
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        // MercadoPago options (binds env vars or configuration)
        services.Configure<MercadoPagoOptions>(configuration);

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            var tokenOptions =
                configuration
                    .GetSection(IdentityTokenOptions.SectionName)
                    .Get<IdentityTokenOptions>() ?? new IdentityTokenOptions();

            options.TokenLifespan = TimeSpan.FromHours(
                tokenOptions.EmailConfirmationTokenExpirationInHours
            );
        });

        services.Configure<DataProtectionTokenProviderOptions>(
            PasswordResetTokenProvider<User>.OptionsName,
            options =>
            {
                var tokenOptions =
                    configuration
                        .GetSection(IdentityTokenOptions.SectionName)
                        .Get<IdentityTokenOptions>() ?? new IdentityTokenOptions();

                options.TokenLifespan = TimeSpan.FromMinutes(
                    tokenOptions.PasswordResetTokenExpirationInMinutes
                );
            }
        );

        // Identity helpers
        services.AddSingleton<ILookupNormalizer, DiacriticsLookupNormalizer>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher>();

        // App services
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEmailQueue, PersistentEmailQueue>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        // Register mailer implementation for both mailer and auth-specific interfaces
        services.AddScoped<QueuedEmailSender>();
        services.AddScoped<Mailers.IEmailSender>(sp => sp.GetRequiredService<QueuedEmailSender>());
        services.AddScoped<AuthEmail.IEmailSender>(sp =>
            sp.GetRequiredService<QueuedEmailSender>()
        );
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        // MercadoPago webhook/service
        services.AddHostedService<PersistentEmailBackgroundService>();

        // ProblemDetails + Swagger
        ProblemDetailsExtensions.AddProblemDetails(services);
        services.AddSwaggerWithBearer();

        // CORS: permite origens definidas em configuration plus quaisquer subdomínios ngrok (*.ngrok-free.app)
        services.AddCors(o =>
        {
            var configured = configuration.GetSection("Cors:Allow").Get<string[]>() ?? Array.Empty<string>();
            o.AddPolicy(
                "Allowlist",
                p => p
                    .SetIsOriginAllowed(origin =>
                    {
                        if (string.IsNullOrWhiteSpace(origin)) return false;
                        try
                        {
                            // permitir quaisquer subdomínios do ngrok (ex: *.ngrok-free.app)
                            var host = new Uri(origin).Host;
                            if (host.EndsWith("ngrok-free.app", StringComparison.OrdinalIgnoreCase))
                                return true;

                            // permitir origens explicitamente configuradas
                            return configured.Contains(origin, StringComparer.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
            );
        });

        // FluentEmail SMTP (corrige host "não especificado" se config não carregar)
        var emailOptions =
            configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();

        if (string.IsNullOrWhiteSpace(emailOptions.Host))
            throw new InvalidOperationException(
                "Config Email:Host não carregou. Verifique appsettings e ambiente."
            );

        services
            .AddFluentEmail(emailOptions.FromEmail, emailOptions.FromName)
            .AddRazorRenderer()
            .AddSmtpSender(() =>
            {
                // 587 + StartTLS => EnableSsl = true
                // 465 SSL direto => EnableSsl = true também
                var client = new SmtpClient(emailOptions.Host, emailOptions.Port)
                {
                    EnableSsl = emailOptions.UseStartTls,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                        emailOptions.Username,
                        emailOptions.Password
                    ),
                };

                return client;
            });

        // Auth
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = "IdentityOrBearer";
                options.DefaultAuthenticateScheme = "IdentityOrBearer";
                options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
            })
            .AddPolicyScheme(
                "IdentityOrBearer",
                "Bearer or Cookie",
                options =>
                {
                    options.ForwardDefaultSelector = context =>
                    {
                        var authHeader = context.Request.Headers.Authorization.ToString();
                        return
                            !string.IsNullOrWhiteSpace(authHeader)
                            && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? IdentityConstants.BearerScheme
                            : IdentityConstants.ApplicationScheme;
                    };
                }
            )
            .AddBearerToken(
                IdentityConstants.BearerScheme,
                options =>
                {
                    var tokenOptions =
                        configuration
                            .GetSection(IdentityTokenOptions.SectionName)
                            .Get<IdentityTokenOptions>() ?? new IdentityTokenOptions();

                    options.BearerTokenExpiration = TimeSpan.FromMinutes(
                        tokenOptions.BearerTokenExpirationInMinutes
                    );
                    options.RefreshTokenExpiration = TimeSpan.FromDays(
                        tokenOptions.RefreshTokenExpirationInDays
                    );
                }
            )
            .AddIdentityCookies();

        services
            .AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);

                options.Tokens.PasswordResetTokenProvider =
                    PasswordResetTokenProvider<User>.ProviderName;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider<User>>(
                PasswordResetTokenProvider<User>.ProviderName
            );

        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            // When using cross-site requests (ngrok front -> ngrok back), ensure cookie
            // is sent by browser: SameSite=None and Secure are required for modern browsers.
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "RequireAdminL1",
                p => p.RequireRole("ADMIN_L1", "ADMIN_L2", "ADMIN_L3")
            );
            options.AddPolicy("RequireAdminL2", p => p.RequireRole("ADMIN_L2", "ADMIN_L3"));
            options.AddPolicy("RequireAdminL3", p => p.RequireRole("ADMIN_L3"));
        });

        // DB
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
        );

        services.AddHealthChecks();

        return services;
    }
}
