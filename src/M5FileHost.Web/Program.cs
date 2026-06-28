using System.Net;
using System.Threading.RateLimiting;
using M5FileHost.Core;
using M5FileHost.Infrastructure;
using M5FileHost.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddM5Infrastructure(builder.Configuration);
builder.Services.AddOptions<EmailOptions>().Bind(builder.Configuration.GetSection(EmailOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();
builder.Services.AddDataProtection().SetApplicationName("M5FileHost").PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "/data/keys"));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddSignInManager()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppClaimsPrincipalFactory>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-m5filehost";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/forbidden";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    // The SPA expects JSON status codes, not the 302 redirects to /login and
    // /forbidden that the cookie handler emits by default. Return 401/403 for
    // API/XHR requests and keep redirects for any direct browser navigation.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Moderator", policy => policy.RequireRole(nameof(UserRole.Moderator), nameof(UserRole.Admin), nameof(UserRole.Owner)))
    .AddPolicy("Admin", policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Owner)))
    .AddPolicy("Owner", policy => policy.RequireRole(nameof(UserRole.Owner)));
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
// AddControllersWithViews (rather than AddControllers) registers the MVC
// antiforgery filter services that the [ValidateAntiForgeryToken] attributes
// depend on; these previously came in via the now-removed Razor components.
builder.Services.AddControllersWithViews();
builder.Services.Configure<FormOptions>(options =>
{
    options.MemoryBufferThreshold = 64 * 1024;
    options.MultipartBodyLengthLimit = 10L * 1024 * 1024 * 1024;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    options.AddPolicy("uploads", context => RateLimitPartition.GetTokenBucketLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { TokenLimit = 10, TokensPerPeriod = 10, ReplenishmentPeriod = TimeSpan.FromMinutes(1), AutoReplenishment = true, QueueLimit = 0 }));
    options.AddPolicy("reports", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
    options.AddPolicy("downloads", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgres").AddCheck<RedisHealthCheck>("redis");

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownProxies = { IPAddress.Loopback, IPAddress.IPv6Loopback }
});
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com data:; script-src 'self'; connect-src 'self'; frame-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Serve the React SPA for any non-API route. The index.html is rewritten on
// each request to embed a fresh antiforgery request token in its <meta> tag,
// which the client reads and replays as the X-CSRF-TOKEN header on mutations.
var indexHtmlPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
string? indexHtmlTemplate = null;
app.MapFallback(async (HttpContext context, IAntiforgery antiforgery) =>
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/health"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    indexHtmlTemplate ??= await File.ReadAllTextAsync(indexHtmlPath, context.RequestAborted);
    var requestToken = antiforgery.GetAndStoreTokens(context).RequestToken ?? "";
    var meta = $"<meta name=\"csrf-token\" content=\"{System.Net.WebUtility.HtmlEncode(requestToken)}\">";
    var html = System.Text.RegularExpressions.Regex.Replace(indexHtmlTemplate, "<meta name=\"csrf-token\"[^>]*>", _ => meta);
    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.CacheControl = "no-store";
    await context.Response.WriteAsync(html, context.RequestAborted);
});

if (args.Contains("seed-owner", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await OwnerSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
    return;
}

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await OwnerSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}
await app.RunAsync();

public partial class Program;

public sealed class DatabaseHealthCheck(AppDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await database.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
}

public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { await redis.GetDatabase().PingAsync(); return HealthCheckResult.Healthy(); }
        catch (Exception exception) { return HealthCheckResult.Unhealthy("Redis is unavailable.", exception); }
    }
}
