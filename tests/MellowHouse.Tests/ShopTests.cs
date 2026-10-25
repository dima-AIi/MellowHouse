using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MellowHouse.Web.Data;
using MellowHouse.Web.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MellowHouse.Tests;

public sealed class ShopFactory : WebApplicationFactory<Program>
{
    public string DataPath { get; }
    private readonly string environment;
    private readonly bool samples;
    public ShopFactory(string? path = null, string environment = "Development", bool samples=false) { this.environment = environment; this.samples=samples; DataPath = path ?? Path.Combine(FindRoot(), "artifacts", "test-data", Guid.NewGuid().ToString("N")); }
    public static string FindRoot() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MellowHouse.sln"))) dir = dir.Parent; return dir?.FullName ?? throw new InvalidOperationException("Solution not found"); }
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services=>services.AddSingleton<TimeProvider>(new ShopTestClock())).UseEnvironment(environment).UseSetting("Database:DataDirectory", DataPath).UseSetting("Database:ApplyMigrations", "true").UseSetting("Samples:Disabled", (!samples).ToString()).UseSetting("Catalog:Expanded",samples.ToString()).ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{{"Assistant:ApiKey",""}}));
}

public sealed class ShopTestClock:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(DateTime.UtcNow.Date.AddHours(9),TimeSpan.Zero); }

public sealed class Buyer(ShopFactory factory, CookieContainer? cookies = null) : IDisposable
{
    public CookieContainer Cookies { get; } = cookies ?? new();
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("http://localhost") });
    private string token = "";
    public async Task<(HttpStatusCode Status,JsonNode Json)> Upload(byte[] bytes,string name,string kind="product")
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/admin/media");request.Headers.Add("Cookie",Cookies.GetCookieHeader(new Uri("http://localhost")));request.Headers.Add("X-CSRF-TOKEN",token);using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(bytes),"file",name);form.Add(new StringContent(kind),"kind");request.Content=form;using var response=await client.SendAsync(request);return(response.StatusCode,JsonNode.Parse(await response.Content.ReadAsStringAsync())!);
    }
    public async Task<(HttpStatusCode Status, byte[] Bytes)> Raw(string path)
    {
        using var r=new HttpRequestMessage(HttpMethod.Get,path);r.Headers.Add("Cookie",Cookies.GetCookieHeader(new Uri(client.BaseAddress!,path)));using var response=await client.SendAsync(r);return(response.StatusCode,await response.Content.ReadAsByteArrayAsync());
    }
    public async Task<(HttpStatusCode Status, JsonNode Json)> Send(string path, object? body = null, string method = "GET", bool csrf = true)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var cookieHeader = Cookies.GetCookieHeader(new Uri(client.BaseAddress!,path));
        if (!string.IsNullOrEmpty(cookieHeader)) request.Headers.Add("Cookie", cookieHeader);
        if (csrf && method != "GET") request.Headers.Add("X-CSRF-TOKEN", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var values)) foreach (var value in values) Cookies.SetCookies(new Uri("http://localhost"), value);
        var text = await response.Content.ReadAsStringAsync();
        JsonNode json;
        try { json = JsonNode.Parse(text) ?? new JsonObject(); } catch { json = new JsonObject { ["raw"] = text }; }
        return (response.StatusCode, json);
    }
    public async Task<JsonNode> State() { var (status, json) = await Send("/api/state"); Assert.Equal(HttpStatusCode.OK, status); token = json["csrfToken"]!.GetValue<string>(); return json; }
    public async Task SignIn(string? contact = null)
    {
        await State();
        if(contact is not null && !contact.Contains('@')){
            var challenge=await Send("/api/auth/challenge",new{contact,channel="phone"},"POST");Assert.Equal(HttpStatusCode.OK,challenge.Status);
            var result=await Send("/api/auth/verify",new{challengeId=challenge.Json["challengeId"]!.GetValue<string>(),code=challenge.Json["demoCode"]!.GetValue<string>()},"POST");Assert.Equal(HttpStatusCode.OK,result.Status);
        }else{
            contact??=Guid.NewGuid()+"@example.test";var credentials=new{email=contact,password="TestMellow2026!",name="Тестовый покупатель"};
            var result=await Send("/api/auth/register",credentials,"POST");if(result.Status==HttpStatusCode.BadRequest)result=await Send("/api/auth/login",credentials,"POST");Assert.Equal(HttpStatusCode.OK,result.Status);
        }
        await State();
    }
    public async Task<JsonNode> Add(string id, object? selections = null, int quantity = 1)
    {
        var result = await Send("/api/cart", new { productId = id, selections, quantity }, "POST"); Assert.Equal(HttpStatusCode.OK, result.Status); return result.Json;
    }
    public async Task<JsonNode> Checkout(string method = "delivery", string payment = "card", string? key = null)
    {
        var result = await Send("/api/orders", Details(method, payment, key), "POST"); Assert.Equal(HttpStatusCode.OK, result.Status); return result.Json;
    }
    public static object Details(string method = "delivery", string payment = "card", string? key = null) => new { requestKey = key ?? Guid.NewGuid().ToString(), method, city = "Москва", street = "Тестовая, 10", apartment = "12", name = "Тестовый покупатель", phone = "+79990000000", cafe = "demo-house", timing = "asap", scheduledLocal = "", comment = "Тест без реальной доставки", paymentMethod = payment, consent = true };
    public void Dispose() => client.Dispose();
}

public sealed class ShopTests
{
    [Fact] public async Task ProductionDemo_IsolatesProfilesEvenWhenContactIsIdentical()
    {
        using var f=new ShopFactory(environment:"Production");var contact="+79990000099";using var a=new Buyer(f);await a.SignIn(contact);await a.Send("/api/profile",new{name="Приватный профиль"},"PUT");using var b=new Buyer(f);await b.SignIn(contact);var second=await b.State();Assert.Equal("",second["profile"]!["name"]!.GetValue<string>());Assert.Empty((await b.Send("/api/orders")).Json.AsArray());Assert.Equal("Приватный профиль",(await a.State())["profile"]!["name"]!.GetValue<string>());
    }
    [Fact] public async Task OrderSnapshot_KeepsPurchasedPrice_WhileRepeatUsesCurrentCatalog()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("vanilla-raf");var order=await b.Checkout("pickup");var id=Id(order);await b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();(await db.Products.FindAsync("vanilla-raf"))!.Price=399;await db.SaveChangesAsync();}var repeated=await b.Send($"/api/orders/{id}/repeat",new{},"POST");Assert.Equal(399,Amount(repeated.Json["cart"]!,"subtotal"));var original=await b.Send("/api/orders/"+id);Assert.Equal(320,Amount(original.Json,"subtotal"));Assert.Equal(320,Amount(original.Json["items"]![0]!,"unitPrice"));
    }
    [Fact] public async Task ConcurrentCreateAndPayment_DoNotDuplicateOrderOrCharge()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("bbq-chicken");var body=Buyer.Details(key:Guid.NewGuid().ToString());var requests=await Task.WhenAll(b.Send("/api/orders",body,"POST"),b.Send("/api/orders",body,"POST"));Assert.All(requests,x=>Assert.Equal(HttpStatusCode.OK,x.Status));Assert.Equal(Id(requests[0].Json),Id(requests[1].Json));var id=Id(requests[0].Json);var payments=await Task.WhenAll(b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST"),b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST"));Assert.All(payments,x=>Assert.Equal("Paid",x.Json["status"]!.GetValue<string>()));using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();Assert.Equal(1,await db.Orders.CountAsync());Assert.Equal(1,await db.PaymentAttempts.CountAsync());
    }
    private static Dictionary<string, string[]> Oat() => new() { ["size"] = ["300"], ["milk"] = ["oat"], ["sweet"] = ["standard"], ["extras"] = [] };
    private static int Amount(JsonNode node, string path) => node[path]!.GetValue<int>();
    private static string Id(JsonNode node) => node["id"]!.GetValue<string>();
    private static ProductView Raf()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(ShopFactory.FindRoot(), "src/MellowHouse.Web/Data/catalog.json")));
        var p = doc.RootElement.GetProperty("products").EnumerateArray().Single(x => x.GetProperty("id").GetString() == "vanilla-raf");
        return JsonSerializer.Deserialize<ProductView>(p.GetRawText(), JsonDefaults.Options)!;
    }

    [Fact] public void Pricing_UsesApplicableSizeMilkAndEspresso_AndUpdatesAllergens()
    {
        var input = Oat(); var result = Pricing.Calculate(Raf(), input); Assert.Equal(380, result.UnitPrice); Assert.Contains("овёс", result.Allergens); Assert.DoesNotContain("молоко", result.Allergens);
        input["size"] = ["400"]; input["extras"] = ["espresso"]; Assert.Equal(470, Pricing.Calculate(Raf(), input).UnitPrice);
    }
    [Fact] public void Configurations_AreIndependentOfPropertyAndOptionOrder()
    {
        var first = Oat(); first["extras"] = ["espresso", "espresso"];
        var second = new Dictionary<string, string[]> { ["extras"] = ["espresso"], ["sweet"] = ["standard"], ["milk"] = ["oat"], ["size"] = ["300"] };
        Assert.Equal(Pricing.Calculate(Raf(), first).Canonical, Pricing.Calculate(Raf(), second).Canonical);
        Assert.Equal(430, Pricing.Calculate(Raf(), first).UnitPrice);
        Assert.Throws<ShopException>(() => Pricing.Calculate(Raf(), new() { ["milk"] = ["unavailable"] }));
        Assert.Throws<ShopException>(() => Pricing.Calculate(Raf(), new() { ["sauce"] = ["standard"] }));
    }
    [Fact] public async Task Catalog_ContainsTenCategoriesAndExactlyTwoDemoProducts()
    {
        using var f = new ShopFactory(); using var b = new Buyer(f); var catalog = await b.Send("/api/catalog"); Assert.Equal(10, catalog.Json["categories"]!.AsArray().Count); Assert.Equal(2, catalog.Json["products"]!.AsArray().Count);
        var menu = await b.Send("/Menu"); Assert.Equal(HttpStatusCode.OK, menu.Status); Assert.Contains("Скоро в меню", menu.Json["raw"]!.GetValue<string>());
    }
    [Fact] public async Task Cart_MergesCanonicalConfigurations_AndIgnoresClientPrice()
    {
        using var f = new ShopFactory(); using var b = new Buyer(f); await b.State();
        var first = await b.Send("/api/cart", new { productId = "vanilla-raf", selections = Oat(), quantity = 1, unitPrice = 1, total = 1 }, "POST"); Assert.Equal(HttpStatusCode.OK, first.Status);
        var cart = await b.Add("vanilla-raf", Oat()); Assert.Single(cart["items"]!.AsArray()); Assert.Equal(760, Amount(cart,"subtotal"));
        cart = await b.Add("vanilla-raf"); Assert.Equal(2, cart["items"]!.AsArray().Count); Assert.Equal(1080, Amount(cart,"subtotal"));
    }
    [Fact] public async Task Cart_EditMergesOnlyMatchingLine_AndPreservesNeighbor()
    {
        using var f = new ShopFactory(); using var b = new Buyer(f); await b.State(); var cart = await b.Add("vanilla-raf"); var standard = Id(cart["items"]![0]!);
        await b.Add("vanilla-raf", Oat()); await b.Add("bbq-chicken"); var result = await b.Send("/api/cart/"+standard, new { selections = Oat(), quantity = 2 }, "PUT");
        Assert.Equal(HttpStatusCode.OK,result.Status); Assert.Equal(2,result.Json["items"]!.AsArray().Count); Assert.Equal(1530,Amount(result.Json,"subtotal"));
        var raf = result.Json["items"]!.AsArray().Single(x=>x!["product"]!["id"]!.GetValue<string>()=="vanilla-raf")!;
        var removed=await b.Send("/api/cart/"+Id(raf),method:"DELETE"); Assert.Equal(390,Amount(removed.Json,"subtotal"));
    }
    [Fact] public async Task Server_RejectsInvalidOptionsQuantityAndCsrf_WithoutLosingCart()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();await b.Add("bbq-chicken");
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/cart",new{productId="vanilla-raf",selections=new{milk=new[]{"soy"}},quantity=1},"POST")).Status);
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/cart",new{productId="vanilla-raf",quantity=0},"POST")).Status);
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/cart",new{productId="vanilla-raf",quantity=1},"POST",false)).Status);
        Assert.Equal(390,Amount((await b.State())["cart"]!,"subtotal"));
    }
    [Fact] public async Task GuestCanPlacePendingOrder_AndOptionalLoginPreservesCartWithoutClaiming()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();await b.Add("vanilla-raf",Oat());await b.Add("bbq-chicken");
        var guest=await b.Send("/api/orders",Buyer.Details(),"POST");Assert.Equal(HttpStatusCode.OK,guest.Status);Assert.True(guest.Json["guest"]!.GetValue<bool>());
        await b.SignIn();Assert.Empty((await b.Send("/api/orders")).Json.AsArray());Assert.Equal(770,Amount((await b.State())["cart"]!,"subtotal"));var o=await b.Checkout();Assert.Equal(960,Amount(o,"total"));Assert.False(o["guest"]!.GetValue<bool>());
    }
    [Fact] public async Task FavoritesAndExistingCart_MergeAfterLogin_AndStayPrivate()
    {
        using var f=new ShopFactory();var contact=Guid.NewGuid()+"@example.test";using var a=new Buyer(f);await a.SignIn(contact);await a.Add("vanilla-raf");
        using var b=new Buyer(f);await b.State();await b.Add("bbq-chicken");await b.Send("/api/favorites/bbq-chicken",new{selected=true},"PUT");await b.SignIn(contact);var state=await b.State();Assert.Equal(710,Amount(state["cart"]!,"subtotal"));Assert.Single(state["favorites"]!.AsArray());
        using var c=new Buyer(f);Assert.Empty((await c.State())["favorites"]!.AsArray());
    }
    [Fact] public async Task Checkout_ValidatesAddressOnlyForDelivery_AndCafeForPickup()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("vanilla-raf",Oat());
        var invalid=JsonSerializer.SerializeToNode(Buyer.Details())!;invalid["street"]="";Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/orders",invalid,"POST")).Status);
        invalid["method"]="pickup";invalid["city"]="";invalid["cafe"]="demo-house";var valid=await b.Send("/api/orders",invalid,"POST");Assert.Equal(HttpStatusCode.OK,valid.Status);Assert.Equal(380,Amount(valid.Json,"total"));Assert.Equal(0,Amount(valid.Json,"delivery"));
    }
    [Fact] public async Task Payment_ErrorCancelRetry_IsOneOrder_AndPreservesCartUntilSuccess()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("vanilla-raf",Oat());var o=await b.Checkout(payment:"sbp");var id=Id(o);
        foreach(var outcome in new[]{"error","cancel"}){var r=await b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result=outcome},"POST");Assert.Equal(HttpStatusCode.OK,r.Status);Assert.Equal(380,Amount((await b.State())["cart"]!,"subtotal"));}
        var success=await b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");Assert.Equal("Paid",success.Json["status"]!.GetValue<string>());Assert.Equal(0,Amount((await b.State())["cart"]!,"subtotal"));Assert.Single((await b.Send("/api/orders")).Json.AsArray());
    }
    [Fact] public async Task OrderAndPayment_AreIdempotent_ForRepeatedRequests()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("bbq-chicken");var key=Guid.NewGuid().ToString();var one=await b.Checkout(key:key);var two=await b.Checkout(key:key);Assert.Equal(Id(one),Id(two));
        var payment=new{attemptKey=Guid.NewGuid(),result="success"};var id=Id(one);await b.Send($"/api/orders/{id}/payment",payment,"POST");await b.Send($"/api/orders/{id}/payment",payment,"POST");await b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="error"},"POST");
        Assert.Equal("Paid",(await b.Send("/api/orders/"+id)).Json["status"]!.GetValue<string>());using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();Assert.Equal(1,await db.Orders.CountAsync());Assert.Equal(1,await db.PaymentAttempts.CountAsync());
    }
    [Fact] public async Task OtherBuyer_CannotReadPayRepeatOrEditSomeoneElsesOrderAndCart()
    {
        using var f=new ShopFactory();using var a=new Buyer(f);await a.SignIn();var cart=await a.Add("bbq-chicken");var line=Id(cart["items"]![0]!);var order=await a.Checkout();using var b=new Buyer(f);await b.SignIn();var id=Id(order);
        Assert.Equal(HttpStatusCode.NotFound,(await b.Send("/api/orders/"+id)).Status);Assert.Equal(HttpStatusCode.NotFound,(await b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST")).Status);Assert.Equal(HttpStatusCode.NotFound,(await b.Send($"/api/orders/{id}/repeat",new{},"POST")).Status);Assert.Equal(HttpStatusCode.NotFound,(await b.Send("/api/cart/"+line,new{quantity=2},"PUT")).Status);Assert.Equal(390,Amount((await a.State())["cart"]!,"subtotal"));
    }
    [Fact] public async Task UnavailableProduct_BlocksCheckout_WithoutLosingTheLine()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("vanilla-raf");using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();(await db.Products.FindAsync("vanilla-raf"))!.Available=false;await db.SaveChangesAsync();}
        var result=await b.Send("/api/orders",Buyer.Details(),"POST");Assert.Equal(HttpStatusCode.Conflict,result.Status);Assert.Single((await b.State())["cart"]!["items"]!.AsArray());
    }
    [Fact] public async Task PersistentSqliteAndKeys_RestoreCartLoginAndOrderAfterHostRestart()
    {
        string path;CookieContainer cookies;string orderId;
        using(var first=new ShopFactory()){path=first.DataPath;using var b=new Buyer(first);await b.SignIn();await b.Add("vanilla-raf",Oat());orderId=Id(await b.Checkout());cookies=b.Cookies;}
        using var second=new ShopFactory(path);using var restored=new Buyer(second,cookies);var state=await restored.State();Assert.True(state["authenticated"]!.GetValue<bool>());Assert.Equal(380,Amount(state["cart"]!,"subtotal"));Assert.Equal(HttpStatusCode.OK,(await restored.Send("/api/orders/"+orderId)).Status);Assert.Equal(2,(await restored.Send("/api/catalog")).Json["products"]!.AsArray().Count);
    }
    [Fact] public async Task WrongCode_DoesNotAuthenticateOrEraseGuestCart()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();await b.Add("bbq-chicken");var challenge=await b.Send("/api/auth/challenge",new{contact="+79990000098",channel="phone"},"POST");var response=await b.Send("/api/auth/verify",new{challengeId=challenge.Json["challengeId"]!.GetValue<string>(),code="000000"},"POST");Assert.Equal(HttpStatusCode.BadRequest,response.Status);var state=await b.State();Assert.False(state["authenticated"]!.GetValue<bool>());Assert.Equal(390,Amount(state["cart"]!,"subtotal"));
    }
}
