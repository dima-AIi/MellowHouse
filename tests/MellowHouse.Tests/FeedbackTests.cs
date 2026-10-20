using System.Net;
using System.Text.Json.Nodes;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MellowHouse.Tests;

public sealed class FeedbackTests
{
    [Theory][InlineData("")][InlineData(" \r\n ")]
    public async Task WeeklyAndMonthlyReports_HandleLegacyBlankCostsWithoutInventingMargin(string blank)
    {
        using var f=new ShopFactory();using var buyer=new Buyer(f);await buyer.SignIn();await buyer.Add("vanilla-raf");var order=await buyer.Checkout("pickup");var id=order["id"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.OK,(await buyer.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST")).Status);
        await using(var scope=f.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();var stored=await db.Orders.SingleAsync();
            stored.CostsJson=blank;stored.Source="";stored.PaidUtc=DateTime.UtcNow.AddDays(-3);stored.CreatedUtc=stored.PaidUtc.Value;
            (await db.Products.FindAsync("vanilla-raf"))!.Cost=100;await db.SaveChangesAsync();
        }
        using var admin=new Buyer(f);await admin.State();var access=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(f.DataPath,"admin-access.json")))!;
        await admin.Send("/api/auth/admin",new{login=access["login"]!.GetValue<string>(),password=access["password"]!.GetValue<string>()},"POST");await admin.State();
        foreach(var days in new[]{7,30})
        {
            var today=DateTime.UtcNow.AddHours(3).Date;var query=$"?from={today.AddDays(1-days):yyyy-MM-dd}&to={today:yyyy-MM-dd}";
            var response=await admin.Send("/api/admin/report"+query);Assert.Equal(HttpStatusCode.OK,response.Status);
            Assert.Equal(320,response.Json["gross"]!.GetValue<int>());Assert.True(response.Json["missingCost"]!.GetValue<bool>());
            Assert.Null(response.Json["cost"]);Assert.Null(response.Json["margin"]);
            Assert.Equal(HttpStatusCode.OK,(await admin.Raw("/api/admin/report.csv"+query)).Status);
        }
    }

    [Fact] public async Task PersonalDetails_SaveNameAndAddressTogether_PreserveOtherCheckoutFields_AndRejectPartialUpdates()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);var before=await b.State();
        Assert.Equal(HttpStatusCode.Unauthorized,(await b.Send("/api/profile",new{name="Гость",city="Москва",street="Тестовая, 1",saveAddress=true},"PUT")).Status);
        await b.SignIn();var name=(await b.State())["profile"]!["name"]!.GetValue<string>();
        await b.Send("/api/checkout-draft",new{city="Москва",street="Старая, 1",comment="Не терять комментарий",paymentMethod="sbp",method="delivery"},"PUT");
        var invalid=await b.Send("/api/profile",new{name="Не сохранять",city="",street="Новая, 2",apartment="",saveAddress=true},"PUT");
        Assert.Equal(HttpStatusCode.BadRequest,invalid.Status);Assert.Equal(name,(await b.State())["profile"]!["name"]!.GetValue<string>());
        Assert.Equal("Старая, 1",(await b.Send("/api/checkout-draft")).Json["street"]!.GetValue<string>());
        var saved=await b.Send("/api/profile",new{name="Анна",city="Москва",street="Новая, 2",apartment="12",saveAddress=true},"PUT");Assert.Equal(HttpStatusCode.OK,saved.Status);
        Assert.Equal("Анна",(await b.State())["profile"]!["name"]!.GetValue<string>());
        var draft=(await b.Send("/api/checkout-draft")).Json;Assert.Equal("Новая, 2",draft["street"]!.GetValue<string>());Assert.Equal("Анна",draft["name"]!.GetValue<string>());
        Assert.Equal("Не терять комментарий",draft["comment"]!.GetValue<string>());Assert.Equal("sbp",draft["paymentMethod"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK,(await b.Send("/api/profile",new{name="Анна",city="",street="",apartment="",saveAddress=true},"PUT")).Status);
        Assert.Equal("",(await b.Send("/api/checkout-draft")).Json["street"]!.GetValue<string>());
    }
}
