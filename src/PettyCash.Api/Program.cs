using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PettyCash.Api;
using PettyCash.Api.Security;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Common;
using PettyCash.Infrastructure;
using PettyCash.Infrastructure.Email;
using PettyCash.Infrastructure.Security;
using PettyCash.Infrastructure.Identity;
using PettyCash.Infrastructure.Persistence;
using PettyCash.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

var listenPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(listenPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{listenPort}");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 12 * 1024 * 1024;
});

if (!builder.Environment.IsDevelopment())
{
    var connectionString = PostgresConnectionStrings.Normalize(
        builder.Configuration.GetConnectionString("DefaultConnection"));
    if (connectionString.Length > 0)
    {
        builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
    }
}

ProductionSecurity.Validate(builder.Configuration, builder.Environment);

if (!builder.Environment.IsDevelopment())
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
    });
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(new { message = ClientErrors.InvalidDetails });
    });
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        if (origins.Length == 0 && builder.Environment.IsDevelopment())
        {
            origins = ["http://localhost:3000"];
        }

        policy.WithOrigins(origins)
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete)
            .WithHeaders("Content-Type", "Authorization", ProductionSecurity.FetchHeader)
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many attempts. Wait a minute and try again." },
            cancellationToken);
    };
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitKey(httpContext, builder.Configuration),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var jwtKey = ProductionSecurity.RequireJwtKey(builder.Configuration, builder.Environment);
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PettyCash",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PettyCash",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token)
                    && context.Request.Cookies.TryGetValue(AuthCookies.Name, out var cookie)
                    && !string.IsNullOrWhiteSpace(cookie))
                {
                    context.Token = cookie;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole(AppRoles.Admin));
    options.AddPolicy("Approver", p => p.RequireRole(AppRoles.Approver, AppRoles.Admin));
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var failure = context.Features.Get<IExceptionHandlerFeature>();
        if (failure?.Error is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("PettyCash.Api");
            logger.LogError(failure.Error, "Unhandled API error");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = ClientErrors.Unexpected });
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
    headers["Cache-Control"] = "no-store";
    await next();
});

if (app.Environment.IsDevelopment())
{
    DevPostgresBootstrap.EnsureStarted(
        app.Logger,
        builder.Configuration.GetConnectionString("DefaultConnection"));
}

if (!app.Environment.IsDevelopment()
    && (args.Contains("--export-staff-passwords") || args.Contains("--reset-all-passwords")))
{
    Console.Error.WriteLine("Password export and reset are disabled outside Development.");
    return;
}

if (args.Contains("--export-staff-passwords"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PettyCashDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<StaffDataSeeder>();
    await seeder.ExportTemporaryPasswordsAsync();
    return;
}

var resetPasswordIndex = Array.IndexOf(args, "--reset-all-passwords");
if (resetPasswordIndex >= 0)
{
    var temporaryPassword = resetPasswordIndex + 1 < args.Length ? args[resetPasswordIndex + 1] : null;
    if (string.IsNullOrWhiteSpace(temporaryPassword) || temporaryPassword.StartsWith('-'))
    {
        Console.Error.WriteLine("Usage: --reset-all-passwords <temporary-password>");
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PettyCashDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<StaffDataSeeder>();
    var reset = await seeder.ResetAllPasswordsAsync(temporaryPassword);
    Console.WriteLine($"Reset {reset} accounts. Each person must change the password at sign-in.");
    return;
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PettyCashDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<StaffDataSeeder>();
    await seeder.SeedAsync();
    EmailTransportDiagnostics.LogActiveTransport(scope.ServiceProvider, app.Logger);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/health"))
    {
        await next();
        return;
    }

    if (!app.Environment.IsDevelopment())
    {
        var configured = builder.Configuration["Proxy:Secret"] ?? "";
        var presented = context.Request.Headers[ProductionSecurity.ProxyHeader].ToString();
        if (!ProductionSecurity.SecretMatches(configured, presented))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }

    if (HttpMethods.IsPost(context.Request.Method)
        || HttpMethods.IsPut(context.Request.Method)
        || HttpMethods.IsDelete(context.Request.Method))
    {
        var fetchHeader = context.Request.Headers[ProductionSecurity.FetchHeader].ToString();
        if (!string.Equals(fetchHeader, ProductionSecurity.FetchHeaderValue, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "This action was blocked. Refresh the page and try again." });
            return;
        }
    }

    await next();
});

app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var audit = context.RequestServices.GetRequiredService<AuditUserContext>();
    audit.UserId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();

static string RateLimitKey(HttpContext httpContext, IConfiguration configuration)
{
    var configured = configuration["Proxy:Secret"];
    var presented = httpContext.Request.Headers[ProductionSecurity.ProxyHeader].ToString();
    if (!string.IsNullOrWhiteSpace(configured) && ProductionSecurity.SecretMatches(configured, presented))
    {
        var client = httpContext.Request.Headers[ProductionSecurity.ClientIpHeader].ToString();
        if (!string.IsNullOrWhiteSpace(client))
        {
            return client;
        }
    }

    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public partial class Program;
