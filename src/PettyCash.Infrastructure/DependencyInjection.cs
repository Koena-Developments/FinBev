using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Options;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Email;
using PettyCash.Infrastructure.Graph;
using PettyCash.Infrastructure.HostedServices;
using PettyCash.Infrastructure.Identity;
using PettyCash.Infrastructure.Persistence;
using PettyCash.Infrastructure.Seed;
using PettyCash.Infrastructure.Services;

namespace PettyCash.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<PettyCashDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<PettyCashDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICashRequestService, CashRequestService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IPettyCashFormService, PettyCashFormService>();
        services.Configure<PettyCashFormOptions>(configuration.GetSection(PettyCashFormOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<IRequestNotificationService, RequestNotificationService>();

        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        var useDelegatedGraph = emailSection.GetValue<bool>("UseDelegatedGraph")
            && emailSection.GetValue<bool>("UseMicrosoftGraph")
            && !string.IsNullOrWhiteSpace(configuration["Graph:TenantId"])
            && !string.IsNullOrWhiteSpace(configuration["Graph:ClientId"]);
        var graphAppOnly = emailSection.GetValue<bool>("UseMicrosoftGraph")
            && !emailSection.GetValue<bool>("UseDelegatedGraph")
            && !string.IsNullOrWhiteSpace(emailSection["GraphSenderUpn"])
            && !string.IsNullOrWhiteSpace(configuration["Graph:TenantId"])
            && !string.IsNullOrWhiteSpace(configuration["Graph:ClientId"])
            && !string.IsNullOrWhiteSpace(configuration["Graph:ClientSecret"]);
        var smtpEnabled = emailSection.GetValue<bool>("Enabled")
            && !string.IsNullOrWhiteSpace(emailSection["SmtpHost"]);

        services.AddSingleton<GraphMailboxTokenProvider>();

        var isDevelopment = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);

        // Local runs must not deliver mail, even when Graph or SMTP secrets are present.
        if (isDevelopment)
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }
        else if (useDelegatedGraph)
        {
            services.AddScoped<IEmailSender, DelegatedGraphEmailSender>();
        }
        else if (graphAppOnly)
        {
            services.AddScoped<IEmailSender, GraphEmailSender>();
        }
        else if (smtpEnabled)
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }
        services.AddScoped<IFloatService, FloatService>();
        services.AddScoped<IFinanceDashboardService, FinanceDashboardService>();
        services.AddScoped<IManagerDashboardService, ManagerDashboardService>();
        services.AddScoped<IMasterDataService, MasterDataService>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddScoped<IReceiptReminderService, ReceiptReminderService>();
        services.AddSingleton<IMailboxConnectionLookup, GraphMailboxConnectionLookup>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IApprovalRoutingService, ApprovalRoutingService>();
        services.AddScoped<StaffDataSeeder>();
        services.AddScoped<PasswordResetMailer>();
        services.AddScoped<AuditUserContext>();

        var graphConfigured = !string.IsNullOrWhiteSpace(configuration["Graph:TenantId"])
            && !string.IsNullOrWhiteSpace(configuration["Graph:ClientId"])
            && !string.IsNullOrWhiteSpace(configuration["Graph:ClientSecret"]);

        if (graphConfigured)
        {
            services.AddScoped<ILeaveStatusClient, GraphLeaveStatusClient>();
        }
        else
        {
            services.AddScoped<ILeaveStatusClient, NullLeaveStatusClient>();
        }

        services.AddHostedService<ApproverAvailabilitySyncHostedService>();
        if (!isDevelopment)
        {
            services.AddHostedService<ReceiptReminderHostedService>();
        }

        return services;
    }
}
