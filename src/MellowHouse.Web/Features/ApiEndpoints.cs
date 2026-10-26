using System.Text.Json;
using MellowHouse.Web.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Features;

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/state", async (HttpContext h, ShopDbContext db, CartService cart, IAntiforgery csrf, CancellationToken ct) =>
        {
            var owner = IdentityContext.Owner(h);
            Customer? c = h.User.Identity?.IsAuthenticated == true ? await db.Customers.FindAsync([IdentityContext.Customer(h)], ct) : null;
            var savedMethod=ReadMethod((await db.CheckoutDrafts.FindAsync([owner],ct))?.Json);
            return Results.Ok(new { fulfillmentMethod=savedMethod, csrfToken = csrf.GetAndStoreTokens(h).RequestToken, authenticated = c is not null,
                profile = c is null ? null : new { c.Id, c.Name, c.Contact, c.Role, c.BirthDate, c.DeliveryPhone, hasPassword=c.PasswordHash is not null }, cart = await cart.ViewAsync(owner, ct),
                favorites = await db.Favorites.Where(x => x.Owner == owner).Select(x => x.ProductId).ToArrayAsync(ct),
                deliveryPrice = (await h.RequestServices.GetRequiredService<SiteService>().GetAsync(ct)).DeliveryPrice, settings=await h.RequestServices.GetRequiredService<SiteService>().GetAsync(ct), isolatedDemo = !app.Environment.IsDevelopment() });
        });
        app.MapGet("/api/catalog", async (ShopDbContext db, CancellationToken ct) => Results.Ok(new {
            categories = await db.Categories.Where(x=>x.Visible&&!x.Archived).OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.Name, x.SortOrder,x.Image,x.OnHome }).ToArrayAsync(ct),
            products = (await db.Products.Include(x=>x.Category).Where(x=>!x.Draft&&!x.Archived&&x.Category.Visible&&!x.Category.Archived).OrderBy(x => x.SortOrder).ToArrayAsync(ct)).Select(x => x.View()) }));
        app.MapGet("/api/products/{id}", async (string id, ShopDbContext db, CancellationToken ct) =>
            await db.Products.Include(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==id&&!x.Archived&&!x.Draft&&x.Category.Visible&&!x.Category.Archived,ct) is Product p ? Results.Ok(p.View()) : Results.NotFound(new { error = "Товар не найден." }));
        app.MapPost("/api/products/{id}/price", async (string id, Dictionary<string, string[]> input, ShopDbContext db, CancellationToken ct) =>
            Results.Ok(Pricing.Calculate((await db.Products.Include(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new ShopException("Товар не найден.", 404)).View(), input)));
        app.MapGet("/api/cart", async (HttpContext h, CartService c, CancellationToken ct) => Results.Ok(await c.ViewAsync(IdentityContext.Owner(h), ct)));
        app.MapPost("/api/cart/browser-sync",async(BrowserCartRequest r,HttpContext h,BrowserCartSync sync,CancellationToken ct)=>
            Results.Ok(await sync.MergeAsync(IdentityContext.Owner(h),r,ct)));
        app.MapPost("/api/cart/product/{id}",async(string id,ProductQuantityChange r,HttpContext h,CartService c,CancellationToken ct)=>
        {await c.AdjustProductAsync(IdentityContext.Owner(h),id,r.Delta,ct);return Results.Ok(await c.ViewAsync(IdentityContext.Owner(h),ct));});
        app.MapPost("/api/cart", async (CartChange r, HttpContext h, CartService c, CancellationToken ct) =>
        { await c.AddAsync(IdentityContext.Owner(h), r, ct); return Results.Ok(await c.ViewAsync(IdentityContext.Owner(h), ct)); });
        app.MapPut("/api/cart/{id:guid}", async (Guid id, CartEdit r, HttpContext h, CartService c, CancellationToken ct) =>
        { await c.EditAsync(IdentityContext.Owner(h), id, r, ct); return Results.Ok(await c.ViewAsync(IdentityContext.Owner(h), ct)); });
        app.MapDelete("/api/cart/{id:guid}", async (Guid id, HttpContext h, CartService c, CancellationToken ct) =>
        { await c.RemoveAsync(IdentityContext.Owner(h), id, ct); return Results.Ok(await c.ViewAsync(IdentityContext.Owner(h), ct)); });
        app.MapDelete("/api/cart", async (HttpContext h, CartService c, CancellationToken ct) =>
        { await c.RemoveAsync(IdentityContext.Owner(h), null, ct); return Results.Ok(await c.ViewAsync(IdentityContext.Owner(h), ct)); });
        app.MapPut("/api/favorites/{id}", async (string id, FavoriteRequest r, HttpContext h, ShopDbContext db, CancellationToken ct) =>
        {
            if (!await db.Products.AnyAsync(x => x.Id == id, ct)) throw new ShopException("Товар не найден.", 404);
            var owner = IdentityContext.Owner(h); var f = await db.Favorites.FindAsync([owner, id], ct);
            if (r.Selected && f is null) db.Favorites.Add(new Favorite { Owner = owner, ProductId = id });
            if (!r.Selected && f is not null) db.Favorites.Remove(f);
            await db.SaveChangesAsync(ct); return Results.Ok(new { selected = r.Selected });
        });
        app.MapPost("/api/auth/challenge", async (ChallengeRequest r, HttpContext h, IdentityService s, CancellationToken ct) => Results.Ok(await s.ChallengeAsync(h, r, ct))).RequireRateLimiting("login");
        app.MapPost("/api/auth/verify", async (VerifyRequest r, HttpContext h, IdentityService s, CancellationToken ct) =>
        { await s.VerifyAsync(h, r, ct); return Results.Ok(new { authenticated = true }); }).RequireRateLimiting("login");
        app.MapPost("/api/auth/register",async(CredentialsRequest r,HttpContext h,IdentityService s,CancellationToken ct)=>{await s.RegisterAsync(h,r,ct);return Results.Ok(new{authenticated=true});}).RequireRateLimiting("login");
        app.MapPost("/api/auth/login",async(CredentialsRequest r,HttpContext h,IdentityService s,CancellationToken ct)=>{await s.LoginAsync(h,r,false,ct);return Results.Ok(new{authenticated=true});}).RequireRateLimiting("login");
        app.MapPost("/api/auth/admin",async(CredentialsRequest r,HttpContext h,IdentityService s,CancellationToken ct)=>{await s.LoginAsync(h,r,true,ct);return Results.Ok(new{authenticated=true});}).RequireRateLimiting("login");
        app.MapPost("/api/auth/forgot",async(ForgotRequest r,IdentityService s,CancellationToken ct)=>{await s.ForgotAsync(r.Email,ct);return Results.Ok(new{message="Если профиль существует, инструкция восстановления подготовлена. В локальной версии она доступна администратору кафе."});}).RequireRateLimiting("login");
        app.MapPost("/api/auth/reset",async(ResetPasswordRequest r,HttpContext h,IdentityService s,CancellationToken ct)=>{await s.ResetAsync(r,ct);await h.SignOutAsync();return Results.Ok(new{changed=true});}).RequireRateLimiting("login");
        app.MapPost("/api/auth/logout", async (HttpContext h) => { await h.SignOutAsync(); h.Response.Cookies.Delete("mh.guest"); return Results.Ok(new { authenticated = false }); });
        app.MapPut("/api/profile", async (ProfileRequest r, HttpContext h, ShopDbContext db, CancellationToken ct) =>
        {
            if ((r.Name ?? "").Length > 80) throw new ShopException("Имя должно быть короче 80 символов.", field: "name");
            var c = await db.Customers.FindAsync([IdentityContext.Customer(h)], ct) ?? throw new ShopException("Сеанс завершён. Войдите снова.", 401);
            if(r.SavePersonal)
            {
                if(!string.IsNullOrEmpty(r.BirthDate) && (!DateOnly.TryParseExact(r.BirthDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var birthday)||birthday>DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3))||birthday<DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddYears(-125)))throw new ShopException("Проверьте дату рождения.",field:"birthday");
                if(!string.IsNullOrWhiteSpace(r.DeliveryPhone))Inputs.Phone(r.DeliveryPhone,"phone");
            }
            if(r.SaveAddress)
            {
                var city=(r.City??"").Trim();var street=(r.Street??"").Trim();var apartment=(r.Apartment??"").Trim();
                if(city.Length>80||street.Length>160||apartment.Length>30)throw new ShopException("Сократите адрес до допустимой длины.",field:"city");
                if((city.Length>0||street.Length>0||apartment.Length>0)&&(city.Length==0||street.Length==0))throw new ShopException("Укажите город, улицу и дом либо очистите весь адрес.",field:city.Length==0?"city":"street");
                if(city.Equals("вне зоны",StringComparison.OrdinalIgnoreCase))throw new ShopException("Этот адрес вне зоны доставки. Выберите другой адрес или самовывоз.",field:"city");
                var owner=IdentityContext.Owner(h);var draft=await db.CheckoutDrafts.FindAsync([owner],ct);
                var json=string.IsNullOrWhiteSpace(draft?.Json)?new System.Text.Json.Nodes.JsonObject():System.Text.Json.Nodes.JsonNode.Parse(draft.Json)!.AsObject();
                json["city"]=city;json["street"]=street;json["apartment"]=apartment;json["name"]=(r.Name??"").Trim();
                if(r.SavePersonal){if(string.IsNullOrWhiteSpace(r.DeliveryPhone))json.Remove("phone");else json["phone"]=Inputs.Phone(r.DeliveryPhone,"phone");}
                if(draft is null){draft=new(){Owner=owner};db.CheckoutDrafts.Add(draft);}
                draft.Json=json.ToJsonString();
            }
            if(r.SavePersonal){c.BirthDate=string.IsNullOrEmpty(r.BirthDate)?null:r.BirthDate;c.DeliveryPhone=string.IsNullOrWhiteSpace(r.DeliveryPhone)?null:Inputs.Phone(r.DeliveryPhone,"phone");}
            c.Name = r.Name?.Trim() ?? ""; await db.SaveChangesAsync(ct); return Results.Ok(new { c.Name, c.Contact });
        });
        app.MapPut("/api/fulfillment",async(MethodRequest r,HttpContext h,ShopDbContext db,CancellationToken ct)=>
        {
            if(r.Method is not("pickup" or "delivery"))throw new ShopException("Выберите самовывоз или доставку.");
            var owner=IdentityContext.Owner(h);var draft=await db.CheckoutDrafts.FindAsync([owner],ct);
            var data=System.Text.Json.Nodes.JsonNode.Parse(draft?.Json??"{}")!.AsObject();data["method"]=r.Method;
            if(draft is null){draft=new(){Owner=owner};db.CheckoutDrafts.Add(draft);}draft.Json=data.ToJsonString();await db.SaveChangesAsync(ct);return Results.Ok(new{method=r.Method});
        });
        app.MapGet("/api/checkout-draft", async (HttpContext h, ShopDbContext db, CancellationToken ct) =>
            Results.Content((await db.CheckoutDrafts.FindAsync([IdentityContext.Owner(h)], ct))?.Json ?? "{}", "application/json"));
        app.MapPut("/api/checkout-draft", async (JsonElement r, HttpContext h, ShopDbContext db, CancellationToken ct) =>
        {
            var json = r.GetRawText(); if (json.Length > 6000 || r.ValueKind != JsonValueKind.Object) throw new ShopException("Проверьте данные формы.");
            var owner = IdentityContext.Owner(h); var draft = await db.CheckoutDrafts.FindAsync([owner], ct);
            if (draft is null) { draft = new() { Owner = owner }; db.CheckoutDrafts.Add(draft); }
            draft.Json = json; await db.SaveChangesAsync(ct); return Results.Ok(new { saved = true });
        });
        app.MapPost("/api/orders", async (CheckoutRequest r, HttpContext h, OrderService s, GuestOrderAccess access, CancellationToken ct) =>
            Results.Ok(access.WithAccess(h.User.Identity?.IsAuthenticated == true
                ? await s.CreateAsync(IdentityContext.Customer(h), r, ct)
                : await s.CreateGuestAsync(GuestOrderAccess.Visitor(h), r, ct))));
        app.MapGet("/api/orders", async (HttpContext h, OrderService s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(IdentityContext.Customer(h), ct)));
        app.MapGet("/api/orders/{id:guid}", async (Guid id, HttpContext h, OrderService s, GuestOrderAccess access, CancellationToken ct) => Results.Ok(await s.GetAsync(await access.ResolveAsync(h,id,ct), id, ct)));
        app.MapGet("/api/orders/{id:guid}/qr",async(Guid id,HttpContext h,GuestOrderAccess access,CancellationToken ct)=>{await access.ResolveAsync(h,id,ct);return Results.File(QRCoder.PngByteQRCodeHelper.GetQRCode("MELLOWHOUSE:ORDER:"+id.ToString("N")+":TEST",QRCoder.QRCodeGenerator.ECCLevel.Q,6),"image/png");});
        app.MapPost("/api/orders/{id:guid}/payment", async (Guid id, PaymentRequest r, HttpContext h, OrderService s, GuestOrderAccess access, CancellationToken ct) => Results.Ok(await s.PayAsync(await access.ResolveAsync(h,id,ct), id, r, ct)));
        app.MapPost("/api/orders/{id:guid}/cancel", async (Guid id, CancelOrderRequest r, HttpContext h, OrderService s, GuestOrderAccess access, CancellationToken ct) => Results.Ok(await s.CancelAsync(await access.ResolveAsync(h,id,ct), id, r, ct)));
        app.MapPut("/api/orders/{id:guid}/method", async (Guid id, MethodRequest r, HttpContext h, OrderService s, GuestOrderAccess access, CancellationToken ct) => Results.Ok(await s.ChangeMethodAsync(await access.ResolveAsync(h,id,ct), id, r.Method, ct)));
        app.MapPost("/api/orders/{id:guid}/access",async(Guid id,GuestOrderTokenRequest r,HttpContext h,GuestOrderAccess access,CancellationToken ct)=>
        {await access.OpenAsync(h,id,r.Token,ct);return Results.Ok(new{opened=true});}).RequireRateLimiting("login");
        app.MapPost("/api/orders/{id:guid}/access-link",async(Guid id,HttpContext h,OrderService s,GuestOrderAccess access,CancellationToken ct)=>
            Results.Ok(access.WithAccess(await s.GetAsync(await access.ResolveAsync(h,id,ct),id,ct))));
        app.MapPost("/api/orders/{id:guid}/claim",async(Guid id,HttpContext h,GuestOrderAccess access,CancellationToken ct)=>
        {await access.ClaimAsync(h,id,ct);return Results.Ok(new{saved=true});});
        app.MapPost("/api/orders/{id:guid}/repeat", async (Guid id, HttpContext h, OrderService s, CartService c, CancellationToken ct) =>
        { var uid = IdentityContext.Customer(h); var warnings = await s.RepeatAsync(uid, id, ct); return Results.Ok(new { warnings, cart = await c.ViewAsync("u:" + uid, ct) }); });
        app.MapPost("/api/assistant",async(AssistantRequest r,HttpContext h,CafeAssistantService assistant,CancellationToken ct)=>Results.Ok(await assistant.ReplyAsync(r,IdentityContext.Owner(h),ct))).RequireRateLimiting("assistant");
        AdminApiEndpoints.Map(app);
    }
    private static string ReadMethod(string? json)
    {
        if(string.IsNullOrWhiteSpace(json))return "pickup";
        using var doc=JsonDocument.Parse(json);return doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("method",out var value)&&value.ValueKind==JsonValueKind.String&&value.GetString()=="delivery"?"delivery":"pickup";
    }
    public sealed record FavoriteRequest(bool Selected);
    public sealed record MethodRequest(string Method);
    public sealed record ProfileRequest(string? Name,string? City=null,string? Street=null,string? Apartment=null,bool SaveAddress=false,string? BirthDate=null,string? DeliveryPhone=null,bool SavePersonal=false);
    public sealed record ForgotRequest(string? Email);
}
