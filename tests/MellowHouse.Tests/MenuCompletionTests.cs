using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using MellowHouse.Web.Data;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MellowHouse.Tests;

public sealed class MenuCompletionTests
{
    [Fact]
    public async Task FullMenu_CoversEveryAgreedProduct_AndFiveNewDrinks()
    {
        using var f=new ShopFactory(samples:true);using var b=new Buyer(f);
        var menu=(await b.Send("/api/catalog")).Json;
        var products=menu["products"]!.AsArray();var ids=products.Select(p=>p!["id"]!.GetValue<string>()).ToHashSet();
        using var planned=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(ShopFactory.FindRoot(),"content-staging","catalog-full.json")));
        Assert.Equal(78,products.Count);Assert.Equal(10,menu["categories"]!.AsArray().Count);
        Assert.All(planned.RootElement.GetProperty("products").EnumerateArray(),p=>Assert.Contains(p.GetProperty("id").GetString()!,ids));
        foreach(var id in new[]{"strawberry-matcha","mango-matcha","bubblegum-shake","taro-bubble-tea","mango-bubble-tea"})Assert.Contains(id,ids);
        var expected=new Dictionary<string,int>{{"breakfast",6},{"coffee",10},{"tea",6},{"cold",12},{"shakes",7},{"sandwiches",8},{"salads",8},{"hot",9},{"pizza",4},{"desserts",8}};
        foreach(var (category,count) in expected)Assert.Equal(count,products.Count(p=>p!["categoryId"]!.GetValue<string>()==category));
        Assert.All(products,p=>{Assert.NotEmpty(p!["ingredients"]!.GetValue<string>());Assert.NotEmpty(p["description"]!.GetValue<string>());Assert.NotEmpty(p["portion"]!.GetValue<string>());});
    }

    [Fact]
    public async Task Expansion_PreservesExistingProductsCartCustomerAndOrder_AndDoesNotAddSamples()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("bbq-chicken");await b.Checkout("pickup");
        await using var scope=f.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var raf=(await db.Products.FindAsync("vanilla-raf"))!;raf.Name="Мой раф";raf.Price=777;raf.Archived=true;await db.SaveChangesAsync();
        var old=await db.Products.AsNoTracking().OrderBy(p=>p.Id).ToArrayAsync();var snapshot=JsonSerializer.Serialize(old);var order=JsonSerializer.Serialize(await db.Orders.AsNoTracking().SingleAsync());var cart=JsonSerializer.Serialize(await db.CartLines.AsNoTracking().ToArrayAsync());var customer=JsonSerializer.Serialize(await db.Customers.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync());
        await CafeExpansion.SeedAsync(scope.ServiceProvider,Path.Combine(ShopFactory.FindRoot(),"src","MellowHouse.Web"),includeSamples:false);
        Assert.Equal(78,await db.Products.CountAsync());Assert.Equal(snapshot,JsonSerializer.Serialize(await db.Products.AsNoTracking().Where(p=>p.Id=="vanilla-raf"||p.Id=="bbq-chicken").OrderBy(p=>p.Id).ToArrayAsync()));
        Assert.Equal(order,JsonSerializer.Serialize(await db.Orders.AsNoTracking().SingleAsync()));Assert.Equal(cart,JsonSerializer.Serialize(await db.CartLines.AsNoTracking().ToArrayAsync()));Assert.Equal(customer,JsonSerializer.Serialize(await db.Customers.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync()));
        var item=(await db.Products.FindAsync("strawberry-matcha"))!;item.Price=999;item.Archived=true;await db.SaveChangesAsync();
        await CafeExpansion.SeedAsync(scope.ServiceProvider,Path.Combine(ShopFactory.FindRoot(),"src","MellowHouse.Web"),includeSamples:false);Assert.Equal(78,await db.Products.CountAsync());Assert.Equal(999,item.Price);Assert.True(item.Archived);Assert.Single(await db.Orders.ToArrayAsync());
    }

    [Fact]
    public async Task NewDrinks_PriceOptionsAndMilkAllergens_AreCalculatedByServer()
    {
        using var f=new ShopFactory(samples:true);using var b=new Buyer(f);await b.State();
        var picks=new Dictionary<string,string[]>{{"size",["500"]},{"milk",["oat"]}};
        var price=await b.Send("/api/products/strawberry-matcha/price",picks,"POST");Assert.Equal(HttpStatusCode.OK,price.Status);Assert.Equal(520,price.Json["unitPrice"]!.GetValue<int>());Assert.DoesNotContain("молоко",price.Json["allergens"]!.AsArray().Select(x=>x!.GetValue<string>()));Assert.Contains("овёс",price.Json["allergens"]!.AsArray().Select(x=>x!.GetValue<string>()));
        var cart=await b.Add("strawberry-matcha",picks);Assert.Equal(520,cart["subtotal"]!.GetValue<int>());
        var bubble=await b.Add("bubblegum-shake",new Dictionary<string,string[]>{{"size",["500"]},{"cream",["no"]}});Assert.Equal(960,bubble["subtotal"]!.GetValue<int>());
        var taro=(await b.Send("/api/products/taro-bubble-tea")).Json;Assert.Contains("тапиока",taro["ingredients"]!.GetValue<string>(),StringComparison.OrdinalIgnoreCase);
        var shake=(await b.Send("/api/products/bubblegum-shake")).Json;Assert.DoesNotContain(shake["groups"]!.AsArray().SelectMany(g=>g!["options"]!.AsArray()),o=>o!["label"]!.GetValue<string>()=="Без сахара");
    }

    [Fact]
    public async Task Assistant_FindsThreeMatchas_AndAddsNamedNewDrinkOnlyOnRequest()
    {
        using var f=new ShopFactory(samples:true);using var b=new Buyer(f);await b.State();
        var answer=await b.Send("/api/assistant",new{message="Покажи матчу"},"POST");Assert.Equal(HttpStatusCode.OK,answer.Status);
        Assert.Equal(new[]{"mango-matcha","matcha-latte","strawberry-matcha"},answer.Json["productIds"]!.AsArray().Select(x=>x!.GetValue<string>()).Order().ToArray());Assert.Equal(0,(await b.State())["cart"]!["count"]!.GetValue<int>());
        answer=await b.Send("/api/assistant",new{message="Добавь бабл гам в корзину",requestId=Guid.NewGuid()},"POST");Assert.Equal(HttpStatusCode.OK,answer.Status);Assert.Equal("bubblegum-shake",answer.Json["cart"]!["items"]![0]!["product"]!["id"]!.GetValue<string>());
        answer=await b.Send("/api/assistant",new{message="Добавь гранолу",requestId=Guid.NewGuid()},"POST");Assert.Equal(HttpStatusCode.OK,answer.Status);Assert.Equal(650,answer.Json["cart"]!["subtotal"]!.GetValue<int>());
    }
}
