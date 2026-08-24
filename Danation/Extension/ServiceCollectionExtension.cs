using DatabaseClass.Models;
using Donation.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.RateLimiting;
using static Donation.Services.LoginServices;

namespace Donation.Extension;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Memory Cache
        services.AddMemoryCache();

        // HTTP Context Accessor
        services.AddHttpContextAccessor();

        // Application Services
        services.AddScoped<EmailService>();
        services.AddScoped<LoginService>();
        services.AddScoped<UserService>();
        services.AddScoped<FileService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<CampaignService>();
        services.AddScoped<DonationService>();

        // FluentEmail Configuration
        var emailFrom = configuration["EmailSettings:From"] ?? "noreply@danation.com";
        var appPassword = configuration["EmailSettings:AppPassword"] ?? "";

        services.AddFluentEmail(emailFrom, "Danation Charity Platform")
            .AddSmtpSender(new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(emailFrom, appPassword),
                EnableSsl = true
            });

        // Authentication & Cookie Configuration
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.LogoutPath = "/Account/Logout";
                options.ExpireTimeSpan = TimeSpan.FromDays(5);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Name = "Danation.Auth";
            });

        // Authorization Policies
        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("ADMIN"))
            .AddPolicy("UserOnly", policy => policy.RequireRole("USER"))
            .AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());

        // Rate Limiter
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync("{\"success\":false,\"message\":\"Too many requests. Please slow down.\"}");
                }
                else
                {
                    context.HttpContext.Response.ContentType = "text/html";
                    await context.HttpContext.Response.WriteAsync(@"
                    <html>
                        <head><title>Too Many Requests</title></head>
                        <body style='text-align:center; font-family:sans-serif; padding-top:80px; background:#f7fafc;'>
                            <h1 style='color:#e53e3e;'>429 - Too Many Requests</h1>
                            <p>Please wait a moment and try again.</p>
                            <a href='/' style='color:#667eea;'>Return to Home</a>
                        </body>
                    </html>");
                }
            };

            options.AddPolicy("RoleBasedPolicy", httpContext =>
            {
                var user = httpContext.User;
                var isAuthenticated = user.Identity?.IsAuthenticated ?? false;

                if (isAuthenticated && user.IsInRole("ADMIN"))
                {
                    var adminId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "admin";
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"Admin_{adminId}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 1000,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                }

                if (isAuthenticated)
                {
                    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.Identity?.Name;
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"User_{userId}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                }

                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "UnknownIP";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"Guest_{clientIp}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}
