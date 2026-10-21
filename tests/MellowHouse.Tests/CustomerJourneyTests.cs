using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MellowHouse.Web.Data;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MellowHouse.Tests;

public sealed class CustomerJourneyTests
{
    static async Task<JsonNode> Ask(Buyer buyer,string message,string[]? ids=null)
    {
        var result=await buyer.Send("/api/assistant",new{message,lastProductIds=ids,requestId=Guid.NewGuid()},"POST");
        Assert.Equal(HttpStatusCode.OK,result.Status);return result.Json;
    }
    [Fact] public async Task History_AnswersYesterdayFromCurrentBuyerOnly_AndOffersActualAvailableFood()
    {
        using var f=new ShopFactory();using var a=new Buyer(f);using var b=new Buyer(f);await a.SignIn();await b.SignIn();
        var customer=Guid.Parse((await a.State())["profile"]!["id"]!.GetValue<string>());
        await using(var scope=f.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            var yesterday=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddDays(-1).ToDateTime(new TimeOnly(12,0),DateTimeKind.Utc).AddHours(-3);
            db.Orders.Add(new(){RequestKey=Guid.NewGuid().ToString(),CustomerId=customer,Status="Paid",Fulfillment="Completed",CreatedUtc=yesterday,LinesJson=JsonSerializer.Serialize(new[]{new OrderLineSnapshot("bbq-chicken","Сэндвич BBQ с курицей","","280 г","{}",["Без лука"],2,390,780)},JsonDefaults.Options)});
            // Another buyer, archived rows and failed payments must never become this buyer's meal history.
            db.Orders.Add(new(){RequestKey=Guid.NewGuid().ToString(),CustomerId=Guid.NewGuid(),Status="Paid",CreatedUtc=yesterday,LinesJson="[]"});
            db.Orders.Add(new(){RequestKey=Guid.NewGuid().ToString(),CustomerId=customer,Status="Paid",Source=DemoBusiness.ArchivedSource,CreatedUtc=yesterday,LinesJson="[]"});
            await db.SaveChangesAsync();
        }
        var answer=await Ask(a,"что я ел вчера");Assert.Contains("Без лука",answer["text"]!.GetValue<string>());Assert.Contains("2 шт.",answer["text"]!.GetValue<string>());
        Assert.Equal("bbq-chicken",Assert.Single(answer["productIds"]!.AsArray())!.GetValue<string>());Assert.Equal(0,(await a.State())["cart"]!["count"]!.GetValue<int>());
        Assert.Empty((await Ask(b,"что я ел вчера"))["productIds"]!.AsArray());
        var add=await Ask(a,"Добавь второй",["vanilla-raf","bbq-chicken"]);Assert.Equal("bbq-chicken",add["cart"]!["items"]![0]!["product"]!["id"]!.GetValue<string>());
    }
    [Fact] public async Task History_GuestAndUnfinishedPayment_GetAnHonestAnswer_NoFabricatedMeals()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();Assert.Contains("Войдите",(await Ask(guest,"хочу как в среду"))["text"]!.GetValue<string>());
        using var a=new Buyer(f);await a.SignIn();await a.Add("vanilla-raf");await a.Checkout(payment:"sbp");
        var pending=await Ask(a,"что я заказывал сегодня");Assert.Contains("незавершённая онлайн-оплата",pending["text"]!.GetValue<string>());Assert.Empty(pending["productIds"]!.AsArray());
        Assert.Equal(1,(await a.State())["cart"]!["count"]!.GetValue<int>());
    }
    [Fact] public void History_DatesUseExplicitCalendarDaysAndPreviousWeek()
    {
        var today=new DateOnly(2026,10,21);
        Assert.True(AssistantHistory.IsQuestion("а в среду?"));
        Assert.True(AssistantHistory.IsQuestion("а вчера?"));
        Assert.Equal(new DateOnly(2026,10,20),AssistantHistory.Day("вчера",today));
        Assert.Equal(new DateOnly(2026,10,19),AssistantHistory.Day("позавчера",today));
        Assert.Equal(new DateOnly(2026,10,14),AssistantHistory.Day("хочу как в среду",today));
        Assert.Equal(new DateOnly(2026,10,16),AssistantHistory.Day("как в пятницу",today));
        Assert.Equal(new DateOnly(2026,10,14),AssistantHistory.Day("в среду на прошлой неделе",today));
        Assert.Equal(new DateOnly(2026,10,18),AssistantHistory.Day("как 18.10.2026",today));
        Assert.Equal(new DateOnly(2026,10,1),AssistantHistory.Day("как 2026-10-01",today));
    }
    [Fact] public async Task CardStepper_PreservesRecipe_RemovesLastPortion_AndNeverChoosesBetweenRecipes()
    {
        using var f=new ShopFactory();using var a=new Buyer(f);using var b=new Buyer(f);await a.State();await b.State();
        var oat=new{size=new[]{"400"},milk=new[]{"oat"},sweet=new[]{"standard"},extras=Array.Empty<string>()};await a.Add("vanilla-raf",oat,2);
        var increased=await a.Send("/api/cart/product/vanilla-raf",new{delta=1},"POST");Assert.Equal(3,increased.Json["count"]!.GetValue<int>());Assert.Equal("oat",increased.Json["items"]![0]!["selections"]!["milk"]![0]!.GetValue<string>());
        await b.Send("/api/cart/product/vanilla-raf",new{delta=-1},"POST");Assert.Equal(3,(await a.State())["cart"]!["count"]!.GetValue<int>());
        await a.Add("vanilla-raf");var ambiguous=await a.Send("/api/cart/product/vanilla-raf",new{delta=-1},"POST");Assert.Equal(HttpStatusCode.Conflict,ambiguous.Status);Assert.Equal("configuration",ambiguous.Json["field"]!.GetValue<string>());
        Assert.Equal(4,(await a.State())["cart"]!["count"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Send("/api/cart/product/vanilla-raf",new{delta=-20},"POST")).Status);
        await b.Add("bbq-chicken");await b.Send("/api/cart/product/bbq-chicken",new{delta=-1},"POST");Assert.Equal(0,(await b.State())["cart"]!["count"]!.GetValue<int>());
    }
    [Fact] public async Task Nutrition_IsOnlyInProductDisclosure_NotHomeOrCatalogCards()
    {
        using var f=new ShopFactory(samples:true);using var b=new Buyer(f);await b.State();
        Assert.DoesNotContain("nutrition-values",Encoding.UTF8.GetString((await b.Raw("/Menu")).Bytes));
        var home=Encoding.UTF8.GetString((await b.Raw("/")).Bytes);Assert.DoesNotContain("nutrition-values",home);Assert.Contains("faq-list",home);
        var detail=Encoding.UTF8.GetString((await b.Raw("/Product/syrniki")).Bytes);Assert.Contains("<summary>КБЖУ и пищевая ценность</summary>",detail);Assert.Contains("nutrition-values",detail);
    }
}
