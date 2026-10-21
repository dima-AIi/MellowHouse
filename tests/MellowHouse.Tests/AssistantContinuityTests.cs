using System.Net;
using System.Text.Json.Nodes;
using MellowHouse.Web.Features;
using Xunit;

namespace MellowHouse.Tests;
public sealed class AssistantContinuityTests
{
    static async Task<JsonNode> Ask(Buyer b,string message)
    {
        var r=await b.Send("/api/assistant",new{message,requestId=Guid.NewGuid()},"POST");Assert.Equal(HttpStatusCode.OK,r.Status);return r.Json;
    }
    static async Task Admin(Buyer b,ShopFactory f){await b.State();var a=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(f.DataPath,"admin-access.json")))!;Assert.Equal(HttpStatusCode.OK,(await b.Send("/api/auth/login",new{email=a["email"]!.GetValue<string>(),password=a["password"]!.GetValue<string>()},"POST")).Status);await b.State();}
    [Fact] public async Task ExplicitNumber_SelectsRequestedOlderOrder_NotLatest_AndNeverLeaksAnotherCustomer()
    {
        using var f=new ShopFactory();using var a=new Buyer(f);await a.SignIn();await a.Add("vanilla-raf");var first=await a.Checkout();var id=first["id"]!.GetValue<string>();
        await a.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");await a.Add("bbq-chicken");var last=await a.Checkout();
        var number=first["number"]!.GetValue<string>();var reply=await Ask(a,"повтори заказ "+number);
        Assert.Contains(number,reply["text"]!.GetValue<string>());Assert.DoesNotContain(last["number"]!.GetValue<string>(),reply["text"]!.GetValue<string>());Assert.Equal("vanilla-raf",Assert.Single(reply["productIds"]!.AsArray())!.GetValue<string>());
        Assert.Equal(1,(await a.State())["cart"]!["count"]!.GetValue<int>());
        using var other=new Buyer(f);await other.SignIn();Assert.Empty((await Ask(other,"Покажи "+number))["productIds"]!.AsArray());
        var missing=await Ask(a,"Повтори MH-999999");Assert.Contains("не найден",missing["text"]!.GetValue<string>());Assert.Empty(missing["productIds"]!.AsArray());
        var pending=await Ask(a,"Покажи "+last["number"]!.GetValue<string>());Assert.Contains("ожидает оплаты",pending["text"]!.GetValue<string>());Assert.Equal("bbq-chicken",Assert.Single(pending["productIds"]!.AsArray())!.GetValue<string>());
        Assert.Equal(1092,AssistantHistory.OrderNumber("заказ МН–1092"));Assert.Equal(1092,AssistantHistory.OrderNumber("1092"));
    }
    [Theory] [InlineData("pickup")] [InlineData("delivery")]
    public async Task StatusChoices_OnlyExposeLegalNextStage_AndCompletionNeedsReceipt(string method)
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("bbq-chicken");var o=await b.Checkout(method,"cash");var id=o["id"]!.GetValue<string>();using var admin=new Buyer(f);await Admin(admin,f);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{id}/status",new{status="Ready"},"PUT")).Status);
        foreach(var next in new[]{"Accepted","Preparing","Ready"})
        {
            var current=(await b.Send("/api/orders/"+id)).Json;Assert.Contains(current["allowedTransitions"]!.AsArray(),n=>n!.GetValue<string>()==next);
            Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/orders/{id}/status",new{status=next},"PUT")).Status);
            Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{id}/status",new{status="New"},"PUT")).Status);
        }
        var courier=await admin.Send($"/api/admin/orders/{id}/status",new{status="OutForDelivery"},"PUT");Assert.Equal(method=="delivery"?HttpStatusCode.OK:HttpStatusCode.Conflict,courier.Status);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{id}/status",new{status="Completed"},"PUT")).Status);
        await admin.Send($"/api/admin/orders/{id}/receipt",new{requestKey=Guid.NewGuid(),method="cash"},"POST");
        Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/orders/{id}/status",new{status="Completed"},"PUT")).Status);
        Assert.Empty((await b.Send("/api/orders/"+id)).Json["allowedTransitions"]!.AsArray());
        Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{id}/status",new{status="Rejected",reason="Поздно"},"PUT")).Status);
    }
}
