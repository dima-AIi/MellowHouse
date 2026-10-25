using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MellowHouse.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Features;

public static class IdentityContext
{
    public static string Guest(HttpContext h) => (string)h.Items["GuestOwner"]!;
    public static Guid Customer(HttpContext h) => Guid.TryParse(h.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new ShopException("Войдите, чтобы продолжить. Корзина сохранена.", 401);
    public static string Owner(HttpContext h) => h.User.Identity?.IsAuthenticated == true ? "u:" + Customer(h) : Guest(h);
    public static Guid Admin(HttpContext h) => h.User.IsInRole("Admin") ? Customer(h) : throw new ShopException("Доступ разрешён только администратору.", h.User.Identity?.IsAuthenticated == true ? 403 : 401);
}
public static class Inputs
{
    public static string Phone(string? value, string field = "phone")
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 24 || !Regex.IsMatch(value, @"^\+?[0-9 ()-]+$")) throw new ShopException("Введите телефон: +7 и десять цифр.", field: field);
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length != 11 || digits[0] is not ('7' or '8')) throw new ShopException("В номере должно быть 11 цифр, начиная с +7 или 8.", field: field);
        return "7" + digits[1..];
    }
    public static string Email(string? value)
    {
        var email = (value ?? "").Trim().ToLowerInvariant();
        if (email.Length > 140 || !new EmailAddressAttribute().IsValid(email)) throw new ShopException("Введите корректную почту.", field: "email");
        return email;
    }
    public static void Password(string? value)
    {
        if (value is null || value.Length is < 10 or > 128 || !value.Any(char.IsLetter) || !value.Any(char.IsDigit)) throw new ShopException("Пароль: от 10 до 128 символов, минимум одна буква и цифра.", field: "password");
    }
    public static string Required(string? value, string label, int max = 160, string? field = null)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0 || value.Length > max) throw new ShopException($"Укажите {label} (до {max} символов).", field: field);
        return value;
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
public sealed record ChallengeRequest(string Contact, string Channel);
public sealed record VerifyRequest(Guid ChallengeId, string Code);
public sealed record CredentialsRequest(string? Email, string? Password, string? Name = null, string? Login = null);
public sealed record ResetPasswordRequest(string Token, string Password);
public sealed class IdentityService(ShopDbContext db, CartService carts, IWebHostEnvironment environment, IPasswordHasher<Customer> hasher, IConfiguration configuration)
{
    public async Task<object> ChallengeAsync(HttpContext h, ChallengeRequest request, CancellationToken ct)
    {
        if (request.Channel != "phone") throw new ShopException("Для входа по почте используйте почту и пароль.", field: "contact");
        var contact = Inputs.Phone(request.Contact, "contact");
        var code = environment.IsDevelopment() ? "123456" : RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var challenge = new AuthChallenge { Owner = IdentityContext.Guest(h), Contact = contact, CodeHash = Inputs.Hash(code), ExpiresUtc = DateTime.UtcNow.AddMinutes(5) };
        db.AuthChallenges.Add(challenge); await db.SaveChangesAsync(ct);
        return new { challengeId = challenge.Id, demoCode = code, expiresIn = 300 };
    }
    public async Task VerifyAsync(HttpContext h, VerifyRequest request, CancellationToken ct)
    {
        var challenge = await db.AuthChallenges.SingleOrDefaultAsync(x => x.Id == request.ChallengeId && x.Owner == IdentityContext.Guest(h), ct);
        if (challenge is null || challenge.Consumed || challenge.ExpiresUtc < DateTime.UtcNow || challenge.Attempts >= 5) throw new ShopException("Код истёк. Получите новый код.", field: "code");
        challenge.Attempts++;
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(challenge.CodeHash), Convert.FromHexString(Inputs.Hash(request.Code ?? ""))))
        { await db.SaveChangesAsync(ct); throw new ShopException("Неверный код. Проверьте шесть цифр.", field: "code"); }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var key = environment.IsDevelopment() ? "dev:" + challenge.Contact : "isolated:" + IdentityContext.Guest(h);
        var c = await db.Customers.SingleOrDefaultAsync(x => x.LoginKey == key, ct);
        if (c is null) { c = new Customer { LoginKey = key, Contact = challenge.Contact }; db.Customers.Add(c); await db.SaveChangesAsync(ct); }
        CheckAccess(c); challenge.Consumed = true; await CompleteSignInAsync(h, c, ct); await tx.CommitAsync(ct);
    }
    public async Task RegisterAsync(HttpContext h, CredentialsRequest r, CancellationToken ct)
    {
        var email = Inputs.Email(r.Email); Inputs.Password(r.Password); var name = Inputs.Required(r.Name, "имя", 80, "name");
        if (await db.Customers.AnyAsync(x => x.EmailNormalized == email || x.Contact == email, ct)) throw new ShopException("Профиль с этой почтой уже существует. Войдите или восстановите доступ.", field: "email");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = new Customer { LoginKey = "email:" + email, EmailNormalized = email, Contact = email, Name = name };
        c.PasswordHash = hasher.HashPassword(c, r.Password!); db.Customers.Add(c); await db.SaveChangesAsync(ct);
        await CompleteSignInAsync(h, c, ct); await tx.CommitAsync(ct);
    }
    public async Task LoginAsync(HttpContext h, CredentialsRequest r, bool admin, CancellationToken ct)
    {
        var login = admin ? Inputs.Required(r.Login, "логин", 140, "login").ToLowerInvariant() : Inputs.Email(r.Email);
        var c = admin ? await db.Customers.SingleOrDefaultAsync(x => x.LoginKey == "admin:" + login && x.Role == "Admin", ct) : await db.Customers.SingleOrDefaultAsync(x => x.EmailNormalized == login, ct);
        if (c?.LockedUntilUtc > DateTime.UtcNow) throw new ShopException("Слишком много попыток. Повторите через 15 минут.", 429);
        var valid = c?.PasswordHash is not null && hasher.VerifyHashedPassword(c, c.PasswordHash, r.Password ?? "") != PasswordVerificationResult.Failed;
        if (!valid)
        {
            if (c is not null) { c.FailedLogins++; if (c.FailedLogins >= 8) c.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15); await db.SaveChangesAsync(ct); }
            throw new ShopException("Неверный логин или пароль.", 400, admin ? "login" : "password");
        }
        CheckAccess(c!); c!.FailedLogins = 0; c.LockedUntilUtc = null;
        await using var tx = await db.Database.BeginTransactionAsync(ct); await CompleteSignInAsync(h, c, ct); await tx.CommitAsync(ct);
    }
    private static void CheckAccess(Customer c) { if (c.Blocked) throw new ShopException("Доступ к профилю приостановлен. Обратитесь в кафе.", 403); }
    private async Task CompleteSignInAsync(HttpContext h, Customer c, CancellationToken ct)
    {
        var visitor=GuestOrderAccess.Visitor(h);
        foreach(var order in await db.Orders.Where(o=>o.CustomerId==visitor&&o.Source=="Guest"&&o.PaidUtc==null).ToArrayAsync(ct))
        {
            var snapshot=System.Text.Json.Nodes.JsonNode.Parse(order.CheckoutJson)!.AsObject();
            if(snapshot.ContainsKey("_cartOwner"))continue;
            snapshot["_cartOwner"]="u:"+c.Id;
            order.CheckoutJson=snapshot.ToJsonString();order.Revision++;
        }
        await carts.MergeAsync(IdentityContext.Guest(h), "u:" + c.Id, ct); c.LastLoginUtc = c.LastActiveUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, c.Id.ToString()), new Claim(ClaimTypes.Name, c.Name), new Claim(ClaimTypes.Role, c.Role), new Claim("session-version", c.SessionVersion.ToString())], CookieAuthenticationDefaults.AuthenticationScheme);
        await h.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }
    public async Task<string> IssueResetAsync(Customer c, Guid? actor, CancellationToken ct)
    {
        if (c.EmailNormalized is null) throw new ShopException("Этот профиль использует телефон и код. Пароль не установлен.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        foreach (var p in await db.PasswordResets.Where(x => x.CustomerId == c.Id && !x.Consumed).ToArrayAsync(ct)) p.Consumed = true;
        db.PasswordResets.Add(new PasswordReset { CustomerId = c.Id, TokenHash = Inputs.Hash(token), ExpiresUtc = DateTime.UtcNow.AddMinutes(30) });
        if (actor.HasValue) db.AuditEntries.Add(new() { ActorId = actor.Value, Action = "password.reset.request", Target = c.Id.ToString() });
        await db.SaveChangesAsync(ct); return "/ResetPassword#" + token;
    }
    public async Task ForgotAsync(string? email, CancellationToken ct)
    {
        var normalized = Inputs.Email(email); var c = await db.Customers.SingleOrDefaultAsync(x => x.EmailNormalized == normalized && !x.Blocked, ct);
        if (c is null) return;
        var url = await IssueResetAsync(c, null, ct);
        if (environment.IsDevelopment())
        {
            var dir = Path.Combine(configuration["Database:DataDirectory"] ?? Path.Combine(environment.ContentRootPath, "App_Data"), "mail"); Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, Guid.NewGuid().ToString("N") + ".txt"), $"Восстановление доступа Mellow House\n{normalized}\nhttp://localhost:5186{url}\nСрок: 30 минут. Одно использование.", ct);
        }
    }
    public async Task ResetAsync(ResetPasswordRequest r, CancellationToken ct)
    {
        Inputs.Password(r.Password);
        if (string.IsNullOrEmpty(r.Token) || r.Token.Length != 64) throw new ShopException("Ссылка недействительна. Запросите новую.");
        var hash = Inputs.Hash(r.Token); var reset = await db.PasswordResets.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (reset is null || reset.Consumed || reset.ExpiresUtc <= DateTime.UtcNow) throw new ShopException("Ссылка недействительна или истекла. Запросите новую.");
        var c = await db.Customers.FindAsync([reset.CustomerId], ct) ?? throw new ShopException("Профиль не найден."); CheckAccess(c);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        c.PasswordHash = hasher.HashPassword(c, r.Password); c.SessionVersion++; c.FailedLogins = 0; c.LockedUntilUtc = null;
        foreach (var t in await db.PasswordResets.Where(x => x.CustomerId == c.Id && !x.Consumed).ToArrayAsync(ct)) t.Consumed = true;
        db.AuditEntries.Add(new() { ActorId = c.Id, Action = "password.changed", Target = c.Id.ToString() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public static async Task ValidateCookieAsync(CookieValidatePrincipalContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<ShopDbContext>();
        if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { context.RejectPrincipal(); return; }
        var c = await db.Customers.FindAsync([id], context.HttpContext.RequestAborted);
        var version = context.Principal?.FindFirstValue("session-version") ?? "0";
        if (c is null || c.Blocked || version != c.SessionVersion.ToString()) { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(); return; }
        if (!context.Principal!.IsInRole(c.Role)) { var identity = (ClaimsIdentity)context.Principal.Identity!; foreach (var claim in identity.FindAll(ClaimTypes.Role).ToArray()) identity.RemoveClaim(claim); identity.AddClaim(new(ClaimTypes.Role, c.Role)); context.ShouldRenew = true; }
        if (c.LastActiveUtc is null || c.LastActiveUtc < DateTime.UtcNow.AddMinutes(-1)) { c.LastActiveUtc = DateTime.UtcNow; await db.SaveChangesAsync(context.HttpContext.RequestAborted); }
    }
}

