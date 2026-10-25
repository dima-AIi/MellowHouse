using System.Net;
using System.Text.Json.Nodes;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MellowHouse.Tests;

public sealed class GuestCheckoutTests
{
    private static string Id(JsonNode order)=>order["id"]!.GetValue<string>();
    private static string Token(JsonNode order)=>order["guestAccessToken"]!.GetValue<string>();

    [Fact]
    public async Task GuestCanOrderAndPay_WithoutAnAccount_AndOnlyPurchasedItemsAreRemoved()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();await guest.Add("bbq-chicken");
        var key=Guid.NewGuid().ToString();var first=await guest.Checkout(key:key);var same=await guest.Checkout(key:key);
        Assert.Equal(Id(first),Id(same));Assert.True(first["guest"]!.GetValue<bool>());Assert.NotEmpty(Token(first));
        Assert.False((await guest.State())["authenticated"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Unauthorized,(await guest.Send("/api/orders")).Status);
        await guest.Add("bbq-chicken");
        var paid=await guest.Send($"/api/orders/{Id(first)}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");
        Assert.Equal(HttpStatusCode.OK,paid.Status);Assert.Equal("Paid",paid.Json["status"]!.GetValue<string>());
        Assert.Equal(1,(await guest.State())["cart"]!["count"]!.GetValue<int>());
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        Assert.Single(await db.Orders.ToArrayAsync());Assert.Empty(await db.Customers.Where(c=>c.Role=="Customer").ToArrayAsync());
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("terminal")]
    public async Task GuestCanChoosePaymentAtReceipt(string method)
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();await guest.Add("bbq-chicken");
        var order=await guest.Checkout("pickup",method);
        Assert.Equal("New",order["fulfillment"]!.GetValue<string>());Assert.Equal(0,(await guest.State())["cart"]!["count"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.OK,(await guest.Send("/api/orders/"+Id(order))).Status);
    }

    [Fact]
    public async Task SamePhoneAndOrderNumberNeverGrantAnotherVisitorsAccess()
    {
        using var f=new ShopFactory();using var owner=new Buyer(f);await owner.State();await owner.Add("bbq-chicken");
        var order=await owner.Checkout();var id=Id(order);
        using var stranger=new Buyer(f);await stranger.State();await stranger.Add("bbq-chicken");await stranger.Checkout();
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.Send("/api/orders/"+id)).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.Raw($"/api/orders/{id}/qr")).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST")).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.Send($"/api/orders/{id}/method",new{method="cash"},"PUT")).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.Send($"/api/orders/{id}/access-link",new{},"POST")).Status);
        await stranger.SignIn();Assert.Equal(HttpStatusCode.NotFound,(await stranger.Send($"/api/orders/{id}/claim",new{},"POST")).Status);
        Assert.Empty((await stranger.Send("/api/orders")).Json.AsArray());
    }

    [Fact]
    public async Task PersonalLinkWorksOnAnotherBrowser_ButOnlyForThatOrder()
    {
        using var f=new ShopFactory();using var owner=new Buyer(f);await owner.State();await owner.Add("bbq-chicken");
        var first=await owner.Checkout();await owner.Send($"/api/orders/{Id(first)}/method",new{method="cash"},"PUT");
        await owner.Add("bbq-chicken");var second=await owner.Checkout();
        using var browser=new Buyer(f);await browser.State();
        Assert.Equal(HttpStatusCode.NotFound,(await browser.Send($"/api/orders/{Id(first)}/access",new{token=Token(first)+"bad"},"POST")).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await browser.Send($"/api/orders/{Id(second)}/access",new{token=Token(first)},"POST")).Status);
        Assert.Equal(HttpStatusCode.OK,(await browser.Send($"/api/orders/{Id(first)}/access",new{token=Token(first)},"POST")).Status);
        var cookie=browser.Cookies.GetCookies(new Uri("http://localhost/api/orders/"+Id(first))).Cast<Cookie>().Single(c=>c.Name.StartsWith("mh.order."));
        Assert.True(cookie.HttpOnly);Assert.Equal("/api/orders/"+Id(first),cookie.Path);
        Assert.Equal(HttpStatusCode.OK,(await browser.Send("/api/orders/"+Id(first))).Status);
        Assert.Equal(HttpStatusCode.OK,(await browser.Raw($"/api/orders/{Id(first)}/qr")).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await browser.Send("/api/orders/"+Id(second))).Status);
        Assert.Equal(HttpStatusCode.Unauthorized,(await browser.Send("/api/orders")).Status);
    }

    [Fact]
    public async Task LoginDoesNotSilentlyClaimGuestOrder_ExplicitClaimAddsItAndRevokesGuestLinks()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();await guest.Add("bbq-chicken");
        var order=await guest.Checkout("pickup","cash");var id=Id(order);
        using var linked=new Buyer(f);await linked.State();await linked.Send($"/api/orders/{id}/access",new{token=Token(order)},"POST");
        Assert.Equal(HttpStatusCode.Unauthorized,(await guest.Send($"/api/orders/{id}/claim",new{},"POST")).Status);
        await guest.SignIn();Assert.Empty((await guest.Send("/api/orders")).Json.AsArray());
        Assert.Equal(HttpStatusCode.OK,(await guest.Send($"/api/orders/{id}/claim",new{},"POST")).Status);
        Assert.Single((await guest.Send("/api/orders")).Json.AsArray());
        Assert.False((await guest.Send("/api/orders/"+id)).Json["guest"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NotFound,(await linked.Send("/api/orders/"+id)).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await linked.Send($"/api/orders/{id}/access",new{token=Token(order)},"POST")).Status);
        Assert.Equal(HttpStatusCode.OK,(await guest.Send($"/api/orders/{id}/claim",new{},"POST")).Status);
    }

    [Fact]
    public async Task PaymentFailuresKeepGuestCart_ThenSwitchingToCashClearsItOnce()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();await guest.Add("bbq-chicken");
        var order=await guest.Checkout();var id=Id(order);
        foreach(var result in new[]{"error","cancel"})
        {
            Assert.Equal(HttpStatusCode.OK,(await guest.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result},"POST")).Status);
            Assert.Equal(1,(await guest.State())["cart"]!["count"]!.GetValue<int>());
        }
        Assert.Equal(HttpStatusCode.OK,(await guest.Send($"/api/orders/{id}/method",new{method="cash"},"PUT")).Status);
        Assert.Equal(0,(await guest.State())["cart"]!["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task LoginBetweenCheckoutAndPayment_ClearsMovedCart_WithoutImplicitlySavingTheOrder()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();await guest.Add("bbq-chicken");
        var order=await guest.Checkout();var id=Id(order);await guest.Add("bbq-chicken");await guest.SignIn();
        Assert.Empty((await guest.Send("/api/orders")).Json.AsArray());
        Assert.Equal(2,(await guest.State())["cart"]!["count"]!.GetValue<int>());
        var paid=await guest.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");
        Assert.Equal(HttpStatusCode.OK,paid.Status);Assert.True(paid.Json["guest"]!.GetValue<bool>());
        Assert.Equal(1,(await guest.State())["cart"]!["count"]!.GetValue<int>());
        Assert.Empty((await guest.Send("/api/orders")).Json.AsArray());
        Assert.Null(paid.Json["checkout"]!["_cartOwner"]);
        Assert.Equal(HttpStatusCode.OK,(await guest.Send($"/api/orders/{id}/claim",new{},"POST")).Status);
        Assert.Single((await guest.Send("/api/orders")).Json.AsArray());
    }

    [Fact]
    public async Task CsrfAndAccountOrderBoundariesRemainEnforced()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();await guest.Add("bbq-chicken");
        Assert.Equal(HttpStatusCode.BadRequest,(await guest.Send("/api/orders",Buyer.Details(),"POST",csrf:false)).Status);
        using var account=new Buyer(f);await account.SignIn();await account.Add("bbq-chicken");var order=await account.Checkout();
        Assert.False(order["guest"]!.GetValue<bool>());Assert.Null(order["guestAccessToken"]);
        Assert.Equal(HttpStatusCode.NotFound,(await guest.Send("/api/orders/"+Id(order))).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await guest.Send($"/api/orders/{Id(order)}/access",new{token="invalid"},"POST")).Status);
    }

    [Fact]
    public async Task AssistantCanReadOwnGuestOrder_ButNeverAnotherGuestOrder()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();await guest.Add("bbq-chicken");
        var order=await guest.Checkout("pickup","cash");
        var response=await guest.Send("/api/assistant",new{message="Покажи мой последний заказ"},"POST");
        Assert.Contains(order["number"]!.GetValue<string>(),response.Json["text"]!.GetValue<string>());
        using var stranger=new Buyer(f);await stranger.State();
        var other=await stranger.Send("/api/assistant",new{message="Покажи "+order["number"]!.GetValue<string>()},"POST");
        Assert.DoesNotContain("BBQ",other.Json["text"]!.GetValue<string>());
        Assert.Contains("Войдите",other.Json["text"]!.GetValue<string>());
    }
}
