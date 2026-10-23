using System.Net;
using System.Threading.RateLimiting;
using MellowHouse.Web.Data;
using MellowHouse.Web.Features;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Configuration.AddJsonFile(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "assistant-secrets.json"), optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var dataDir = builder.Configuration["Database:DataDirectory"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
if (provider == "Postgres")
{
    var connection = builder.Configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Укажите ConnectionStrings__Postgres.");
    builder.Services.AddDbContext<PostgresShopDbContext>(o => o.UseNpgsql(connection));
    builder.Services.AddScoped<ShopDbContext>(s => s.GetRequiredService<PostgresShopDbContext>());
}
else
{
    Directory.CreateDirectory(dataDir);
    var connection = builder.Configuration.GetConnectionString("Sqlite") ?? $"Data Source={Path.Combine(dataDir, "mellowhouse.db")};Default Timeout=30";
    builder.Services.AddDbContext<SqliteShopDbContext>(o => o.UseSqlite(connection));
    builder.Services.AddScoped<ShopDbContext>(s => s.GetRequiredService<SqliteShopDbContext>());
}
builder.Services.AddDataProtection().SetApplicationName("MellowHouse.v1").PersistKeysToDbContext<ShopDbContext>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "mh.account"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; o.ExpireTimeSpan = TimeSpan.FromDays(30); o.SlidingExpiration = true;
    o.LoginPath = "/Account";
    o.Events.OnRedirectToLogin = c => { if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 401; else c.Response.Redirect(c.Request.Path.StartsWithSegments("/Admin")?"/Admin/Login":c.RedirectUri); return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => {c.Response.StatusCode=403;return Task.CompletedTask;};
    o.Events.OnValidatePrincipal = IdentityService.ValidateCookieAsync;
});
builder.Services.AddAuthorization(o=>o.AddPolicy("Admin",p=>p.RequireRole("Admin")));
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "mh.csrf"; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddRazorPages(o=>{o.Conventions.AuthorizeFolder("/Admin","Admin");o.Conventions.AllowAnonymousToPage("/Admin/Login");});
builder.Services.AddScoped<CartService>(); builder.Services.AddScoped<OrderService>(); builder.Services.AddScoped<IdentityService>();
builder.Services.AddSingleton<MutationGate>();
builder.Services.AddScoped<IPasswordHasher<Customer>,PasswordHasher<Customer>>();
builder.Services.AddScoped<SiteService>();builder.Services.AddScoped<MediaService>();builder.Services.AddScoped<CatalogAdminService>();builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<IPaymentProvider,LocalPaymentProvider>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddSingleton<AssistantHealth>();
builder.Services.AddScoped<CafeAssistantService>();
builder.Services.AddScoped<AssistantHistory>();
builder.Services.AddScoped<LocalMenuAssistant>();
builder.Services.AddHttpClient("Nvidia", client => { client.BaseAddress = new Uri("https://integrate.api.nvidia.com/v1/"); client.Timeout = TimeSpan.FromSeconds(45); });
builder.Services.AddScoped<IMenuAssistant,NvidiaMenuAssistant>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("assistant", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        if (IPAddress.TryParse(address, out var ip)) o.KnownProxies.Add(ip);
});
if (!builder.Environment.IsDevelopment()) builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(30); o.IncludeSubDomains = false; });
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Database:ApplyMigrations") || args.Contains("--migrate"))
        await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment() || args.Contains("--seed")) await Seeder.SeedAsync(db, app.Environment.ContentRootPath);
    if(app.Environment.IsDevelopment()||args.Contains("--seed"))await Bootstrap.InitializeAsync(scope.ServiceProvider,app.Environment,app.Configuration);
    if((app.Environment.IsDevelopment()||args.Contains("--seed")) && builder.Configuration.GetValue("Catalog:Expanded",true))
        await CafeExpansion.SeedAsync(scope.ServiceProvider, app.Environment.ContentRootPath, includeSamples:!builder.Configuration.GetValue<bool>("Samples:Disabled"));
    if((app.Environment.IsDevelopment()||args.Contains("--seed")) && builder.Configuration.GetValue("Catalog:Expanded",true) && !builder.Configuration.GetValue<bool>("Samples:Disabled"))await SampleRevision.ApplyAsync(db,dataDir);
    if(args.Contains("--refresh-demo"))await DemoBusiness.RefreshAsync(db,builder.Configuration["Demo:BackupDirectory"]??Path.Combine(dataDir,"backups"));
    if(app.Environment.IsDevelopment()||args.Contains("--seed")||args.Contains("--nutrition"))await NutritionData.SeedAsync(db,app.Environment.ContentRootPath);
}
if (args.Contains("--migrate") || args.Contains("--seed") || args.Contains("--refresh-demo") || args.Contains("--nutrition")) return;
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = ctx => {
    if (ctx.Context.Request.Path.Value is { } path && (path.EndsWith(".js")||path.EndsWith(".css")))
        ctx.Context.Response.Headers.CacheControl = "no-cache";
} });
app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.Use(async (http, next) =>
{
    var protector = http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("MellowHouse.Guest.v1");
    string? guest = null;
    if (http.Request.Cookies.TryGetValue("mh.guest", out var protectedGuest))
    {
        try { var raw = protector.Unprotect(protectedGuest); if (Guid.TryParse(raw, out _)) guest = raw; } catch (System.Security.Cryptography.CryptographicException) { }
    }
    if (guest is null)
    {
        guest = Guid.NewGuid().ToString();
        http.Response.Cookies.Append("mh.guest", protector.Protect(guest), new CookieOptions { HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365), IsEssential = true });
    }
    http.Items["GuestOwner"] = "g:" + guest;
    http.Response.Headers.CacheControl = "no-cache, no-store";
    http.Response.Headers.Pragma = "no-cache";
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    http.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    http.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self'; font-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    var mutation = http.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method);
    var gate = http.RequestServices.GetRequiredService<MutationGate>();
    var serializedMutation = mutation && http.Request.Path != "/api/assistant";
    if (serializedMutation) await gate.Semaphore.WaitAsync(http.RequestAborted);
    try
    {
        if (mutation) await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        await next(http);
    }
    catch (ShopException e) { http.Response.StatusCode = e.Status; await http.Response.WriteAsJsonAsync(new { error = e.Message, field = e.Field }); }
    catch (AntiforgeryValidationException) { http.Response.StatusCode = 400; await http.Response.WriteAsJsonAsync(new { error = "Сессия формы обновилась. Обновите страницу; корзина сохранена." }); }
    catch (DbUpdateConcurrencyException e) { app.Logger.LogWarning(e, "Concurrent change"); http.Response.StatusCode = 409; await http.Response.WriteAsJsonAsync(new { error = "Данные изменились в другой вкладке. Обновите корзину и повторите." }); }
    finally { if (serializedMutation) gate.Semaphore.Release(); }
});
app.MapGet("/health", async (ShopDbContext db, CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
app.MapGet("/media/{name}",async(string name,MediaService media,CancellationToken ct)=>{
    var file=await media.GetAsync("/media/"+name,ct);
    return file is null?Results.NotFound():Results.File(file.Bytes,file.ContentType);
});
ApiEndpoints.Map(app);
app.MapRazorPages();
app.Run();
public partial class Program { }
public sealed class MutationGate { public SemaphoreSlim Semaphore { get; } = new(1, 1); }
