using System.Security.Cryptography;
using MellowHouse.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Features;

/// <summary>Grants access to one guest order; contact details never establish ownership.</summary>
public sealed class GuestOrderAccess(ShopDbContext db, IDataProtectionProvider protection)
{
    private readonly ITimeLimitedDataProtector protector = protection.CreateProtector("MellowHouse.GuestOrder.v1").ToTimeLimitedDataProtector();
    private static string CookieName(Guid id) => "mh.order." + id.ToString("N");
    public static Guid Visitor(HttpContext http) => Guid.Parse(IdentityContext.Guest(http)[2..]);

    public OrderView WithAccess(OrderView order) => order.Guest
        ? order with { GuestAccessToken = protector.Protect(order.Id + ":" + order.Checkout.RequestKey, TimeSpan.FromDays(30)) }
        : order;

    private bool Valid(string? token, Order order)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;
        try { return protector.Unprotect(token, out _) == order.PublicId + ":" + order.RequestKey; }
        catch (CryptographicException) { return false; }
    }

    public async Task<Guid> ResolveAsync(HttpContext http, Guid id, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.PublicId == id && o.Source != DemoBusiness.ArchivedSource, ct);
        if (order is not null)
        {
            if (order.Source == "Guest")
            {
                if (order.CustomerId == Visitor(http) || Valid(http.Request.Cookies[CookieName(id)], order))
                    return order.CustomerId;
            }
            else if (http.User.Identity?.IsAuthenticated == true && order.CustomerId == IdentityContext.Customer(http))
                return order.CustomerId;
        }
        throw new ShopException("Заказ не найден. Откройте сохранённую ссылку или войдите в свой профиль.", 404);
    }

    public async Task OpenAsync(HttpContext http, Guid id, string? token, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.PublicId == id && o.Source == "Guest", ct);
        if (order is null || !Valid(token, order)) throw new ShopException("Ссылка недействительна или истекла. Откройте заказ в исходном браузере либо свяжитесь с кафе.", 404);
        http.Response.Cookies.Append(CookieName(id), token!, new CookieOptions
        {
            HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax,
            Path = "/api/orders/" + id, MaxAge = TimeSpan.FromDays(30), IsEssential = true
        });
    }

    public async Task ClaimAsync(HttpContext http, Guid id, CancellationToken ct)
    {
        var customer = IdentityContext.Customer(http);
        await ResolveAsync(http, id, ct);
        var order = await db.Orders.SingleAsync(o => o.PublicId == id, ct);
        if (order.Source != "Guest") return;
        if (order.PaidUtc is null && !PaymentMethods.AtPickup(order.PaymentMethod))
            throw new ShopException("Сначала завершите оформление и оплату заказа.", 409);
        // A guest's request key must not collide with an existing account checkout.
        order.RequestKey = Guid.NewGuid().ToString();
        order.CustomerId = customer;
        order.Source = "Web";
        order.Revision++;
        db.OrderEvents.Add(new() { OrderId = order.Id, ActorId = customer, Status = order.Fulfillment, Note = "Заказ сохранён клиентом в своём профиле." });
        await db.SaveChangesAsync(ct);
        http.Response.Cookies.Delete(CookieName(id), new CookieOptions { Path = "/api/orders/" + id });
    }
}

public sealed record GuestOrderTokenRequest(string? Token);
