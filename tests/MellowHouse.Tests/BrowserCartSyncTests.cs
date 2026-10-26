using System.Net;
using MellowHouse.Web.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MellowHouse.Tests;

public sealed class BrowserCartSyncTests
{
    static object Draft(string key,string product="bbq-chicken",int quantity=1,int price=390)=>new{requestKey=key,lines=new[]{new{productId=product,selections=new Dictionary<string,string[]>(),quantity,expectedUnitPrice=price}}};

    [Fact]
    public async Task MergePreservesServerCartAndOnlyAddsBrowserSelectionsOnce()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();await b.Add("vanilla-raf");
        var draft=Draft(Guid.NewGuid().ToString(),quantity:3);
        var results=await Task.WhenAll(b.Send("/api/cart/browser-sync",draft,"POST"),b.Send("/api/cart/browser-sync",draft,"POST"));
        Assert.All(results,r=>Assert.Equal(HttpStatusCode.OK,r.Status));var cart=(await b.State())["cart"]!;
        Assert.Equal(4,cart["count"]!.GetValue<int>());Assert.Equal(1490,cart["subtotal"]!.GetValue<int>());Assert.Equal(2,cart["items"]!.AsArray().Count);
        await b.Send("/api/cart/browser-sync",draft,"POST");Assert.Equal(4,(await b.State())["cart"]!["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task UsedBatchCannotBeReplacedWithDifferentContentsAndReceiptsAreOwnerScoped()
    {
        using var f=new ShopFactory();using var a=new Buyer(f);await a.State();var key=Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK,(await a.Send("/api/cart/browser-sync",Draft(key),"POST")).Status);
        Assert.Equal(HttpStatusCode.Conflict,(await a.Send("/api/cart/browser-sync",Draft(key,quantity:2),"POST")).Status);
        using var b=new Buyer(f);await b.State();Assert.Equal(HttpStatusCode.OK,(await b.Send("/api/cart/browser-sync",Draft(key,quantity:2),"POST")).Status);
        Assert.Equal(1,(await a.State())["cart"]!["count"]!.GetValue<int>());Assert.Equal(2,(await b.State())["cart"]!["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task ActualPricesAndAvailabilityWinOverTheCachedCatalogAndWarningsSurviveRetry()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();(await db.Products.FindAsync("bbq-chicken"))!.Available=false;(await db.Products.FindAsync("vanilla-raf"))!.Price=450;await db.SaveChangesAsync();}
        var draft=new{requestKey=Guid.NewGuid(),lines=new[]{new{productId="bbq-chicken",quantity=1,expectedUnitPrice=390},new{productId="vanilla-raf",quantity=1,expectedUnitPrice=320}}};
        var first=await b.Send("/api/cart/browser-sync",draft,"POST");Assert.Equal(HttpStatusCode.OK,first.Status);
        Assert.Equal(1,first.Json["cart"]!["count"]!.GetValue<int>());Assert.Equal(450,first.Json["cart"]!["subtotal"]!.GetValue<int>());
        Assert.Equal(2,first.Json["warnings"]!.AsArray().Count);var repeated=await b.Send("/api/cart/browser-sync",draft,"POST");Assert.Equal(first.Json["warnings"]!.ToJsonString(),repeated.Json["warnings"]!.ToJsonString());
    }

    [Fact]
    public async Task QuantityCapKeepsExistingItemsAndInvalidPayloadDoesNotPartiallyApply()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();await b.Add("bbq-chicken",quantity:95);
        var result=await b.Send("/api/cart/browser-sync",Draft(Guid.NewGuid().ToString(),quantity:10),"POST");Assert.Equal(HttpStatusCode.OK,result.Status);
        Assert.Equal(99,result.Json["cart"]!["count"]!.GetValue<int>());Assert.Single(result.Json["warnings"]!.AsArray());
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/cart/browser-sync",Draft(Guid.NewGuid().ToString(),quantity:0),"POST")).Status);
        Assert.Equal(99,(await b.State())["cart"]!["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task InvalidConfigurationIsExplainedAndCsrfRemainsRequired()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();
        var draft=new{requestKey=Guid.NewGuid(),lines=new[]{new{productId="vanilla-raf",quantity=1,selections=new Dictionary<string,string[]>{{"milk",["missing"]}}}}};
        var result=await b.Send("/api/cart/browser-sync",draft,"POST");Assert.Equal(HttpStatusCode.OK,result.Status);Assert.Empty(result.Json["cart"]!["items"]!.AsArray());Assert.Single(result.Json["warnings"]!.AsArray());
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/cart/browser-sync",Draft(Guid.NewGuid().ToString()),"POST",csrf:false)).Status);
    }
}
