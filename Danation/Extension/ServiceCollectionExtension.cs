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

       
        services.AddScoped<EmailService>();
        services.AddScoped<LoginService>();

        // FluentEmail Configuration
        var emailFrom = configuration["EmailSettings:From"] ?? "noreply@webchatbot.com";
        var appPassword = configuration["EmailSettings:AppPassword"] ?? "";

        services.AddFluentEmail(emailFrom, "Donation System")
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
            });

  
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                // HTML Response ပြန်ပေးမည်ဖြစ်ကြောင်း သတ်မှတ်ခြင်း
                context.HttpContext.Response.ContentType = "text/html";

                // User ကို ပြသချင်သည့် HTML Error Message (သို့မဟုတ် View သို့ Redirect လုပ်နိုင်သည်)
                await context.HttpContext.Response.WriteAsync(@"
            <html>
                <head><title>Too Many Requests</title></head>
                <body style='text-align:center; font-family:sans-serif; padding-top:50px;'>
                    <h1 style='color:red;'>429 - Request တွေ ခဏခဏ ပို့လွန်းနေပါသည်။</h1>
                    <p>ခဏစောင့်ပြီးမှ စာမျက်နှာကို Reload ပြန်လုပ်ပေးပါ။</p>
                    <a href='/'>ပင်မစာမျက်နှာသို့ ပြန်သွားရန်</a>
                </body>
            </html>");
            };

            options.AddPolicy("RoleBasedPolicy", httpContext =>
            {
                var user = httpContext.User;
                var isAuthenticated = user.Identity?.IsAuthenticated ?? false;

                // ၁။ Admin ဖြစ်ပါက
                if (isAuthenticated && user.IsInRole("Admin"))
                {
                    var adminId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.Identity?.Name;

                    // Admin ကို အလွန်မြင့်မားသော Limit ပေးမည် (ဥပမာ - ၁ မိနစ်လျှင် Request ၁၀၀၀)
                    // သို့မဟုတ် Limit လုံးဝ မထားချင်ပါက PermitLimit ကို အလွန်များသော ပမာဏ ပေးထားနိုင်ပါသည်။
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"Admin_{adminId}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 1000,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                }

                // ၂။ ပုံမှန် Login ဝင်ထားသော User ဖြစ်ပါက
                if (isAuthenticated)
                {
                    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.Identity?.Name;

                    // User အတွက် Limit (ဥပမာ - ၁ မိနစ်လျှင် Request ၁၀၀)
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"User_{userId}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                }

                // ၃။ Login မဝင်ထားသော Guest ဖြစ်ပါက
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "UnknownIP";

                // Guest အတွက် Limit (ဥပမာ - ၁ မိနစ်လျှင် Request ၂၀)
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"Guest_{clientIp}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}
