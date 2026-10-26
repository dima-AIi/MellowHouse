using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace MellowHouse.Tests;

public sealed class CustomerCancellationTests
{
    private static string Id(JsonNode order)=>order["id"]!.GetValue<string>();
    private static async Task Admin(Buyer buyer,ShopFactory factory)
    {
        await buyer.State();var access=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(factory.DataPath,"admin-access.json")))!;
        Assert.Equal(HttpStatusCode.OK,(await buyer.Send("/api/auth/admin",new{login=access["login"]!.GetValue<string>(),password=access["password"]!.GetValue<string>()},"POST")).Status);
        await buyer.State();
    }
    private static object Key()=>new{requestKey=Guid.NewGuid()};

    [Fact]
    public async Task PaidGuestCanCancelOnce_RefundIncludesDelivery_AndCannotPayAgain()
    {
        using var factory=new ShopFactory();using var buyer=new Buyer(factory);await buyer.State();await buyer.Add("bbq-chicken");
        var order=await buyer.Checkout();var id=Id(order);await buyer.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");
        await buyer.Add("vanilla-raf");var request=Key();
        var first=await buyer.Send($"/api/orders/{id}/cancel",request,"POST");Assert.Equal(HttpStatusCode.OK,first.Status);
        Assert.Equal("Cancelled",first.Json["fulfillment"]!.GetValue<string>());Assert.Equal("Refunded",first.Json["status"]!.GetValue<string>());
        Assert.Equal(order["total"]!.GetValue<int>(),first.Json["refunded"]!.GetValue<int>());Assert.False(first.Json["canCancel"]!.GetValue<bool>());
        Assert.Empty(first.Json["allowedTransitions"]!.AsArray());Assert.Single(first.Json["refunds"]!.AsArray());
        Assert.Equal(order["delivery"]!.GetValue<int>(),first.Json["refunds"]![0]!["delivery"]!.GetValue<int>());
        foreach(var retry in new[]{request,Key()}){var repeated=await buyer.Send($"/api/orders/{id}/cancel",retry,"POST");Assert.Equal(HttpStatusCode.OK,repeated.Status);Assert.Single(repeated.Json["refunds"]!.AsArray());Assert.Equal(first.Json["events"]!.AsArray().Count,repeated.Json["events"]!.AsArray().Count);}
        Assert.Equal(HttpStatusCode.Conflict,(await buyer.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST")).Status);
        Assert.Equal(HttpStatusCode.Conflict,(await buyer.Send($"/api/orders/{id}/method",new{method="cash"},"PUT")).Status);
        Assert.Equal(1,(await buyer.State())["cart"]!["count"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("card")]
    [InlineData("cash")]
    public async Task UnpaidCancellationNeverCreatesARefundOrChangesTheCurrentCart(string payment)
    {
        using var factory=new ShopFactory();using var buyer=new Buyer(factory);await buyer.State();await buyer.Add("bbq-chicken");
        var order=await buyer.Checkout("pickup",payment);var before=(await buyer.State())["cart"]!["count"]!.GetValue<int>();
        var cancelled=await buyer.Send($"/api/orders/{Id(order)}/cancel",Key(),"POST");Assert.Equal(HttpStatusCode.OK,cancelled.Status);
        Assert.Equal("Cancelled",cancelled.Json["fulfillment"]!.GetValue<string>());Assert.Equal("Cancelled",cancelled.Json["status"]!.GetValue<string>());
        Assert.Equal(0,cancelled.Json["refunded"]!.GetValue<int>());Assert.Empty(cancelled.Json["refunds"]!.AsArray());
        Assert.Equal(before,(await buyer.State())["cart"]!["count"]!.GetValue<int>());
        using var admin=new Buyer(factory);await Admin(admin,factory);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{Id(order)}/status",new{status="Accepted"},"PUT")).Status);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{Id(order)}/receipt",new{requestKey=Guid.NewGuid(),method="cash"},"POST")).Status);
    }

    [Fact]
    public async Task CancellationIsPrivate_RequiresCsrfAndValidKey_AndWorksForAccountOwners()
    {
        using var factory=new ShopFactory();using var buyer=new Buyer(factory);await buyer.SignIn();await buyer.Add("bbq-chicken");var order=await buyer.Checkout();var id=Id(order);
        using var stranger=new Buyer(factory);await stranger.State();
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.Send($"/api/orders/{id}/cancel",Key(),"POST")).Status);
        Assert.Equal(HttpStatusCode.BadRequest,(await buyer.Send($"/api/orders/{id}/cancel",Key(),"POST",csrf:false)).Status);
        Assert.Equal(HttpStatusCode.BadRequest,(await buyer.Send($"/api/orders/{id}/cancel",new{requestKey="bad"},"POST")).Status);
        Assert.True((await buyer.Send("/api/orders/"+id)).Json["canCancel"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.OK,(await buyer.Send($"/api/orders/{id}/cancel",Key(),"POST")).Status);
    }

    [Fact]
    public async Task CancellationAfterPartialRefundReturnsOnlyRemainingQuantitiesAndKeepsReportBalanced()
    {
        using var factory=new ShopFactory();using var buyer=new Buyer(factory);await buyer.SignIn();await buyer.Add("bbq-chicken",quantity:2);var order=await buyer.Checkout();var id=Id(order);
        await buyer.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");
        using var admin=new Buyer(factory);await Admin(admin,factory);
        Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/orders/{id}/refund",new{requestKey=Guid.NewGuid(),reason="Тест частичного возврата",lines=new[]{new{index=0,quantity=1}},delivery=false},"POST")).Status);
        var result=await buyer.Send($"/api/orders/{id}/cancel",Key(),"POST");Assert.Equal(HttpStatusCode.OK,result.Status);
        var refunds=result.Json["refunds"]!.AsArray();Assert.Equal(2,refunds.Count);Assert.Equal(order["total"]!.GetValue<int>(),refunds.Sum(r=>r!["amount"]!.GetValue<int>()));
        Assert.Equal(2,refunds.Sum(r=>r!["lines"]!.AsArray().Sum(l=>l!["quantity"]!.GetValue<int>())));
        var report=(await admin.Send("/api/admin/report")).Json;Assert.Equal(0,report["collected"]!.GetValue<int>());Assert.Equal(0,report["productRevenue"]!.GetValue<int>());Assert.Equal(0,report["newOrders"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("Ready",true)]
    [InlineData("OutForDelivery",true)]
    [InlineData("Completed",false)]
    public async Task ActivePaidOrdersCanBeCancelled_ButDeliveredOrdersCannot(string finalStage,bool allowed)
    {
        using var factory=new ShopFactory();using var buyer=new Buyer(factory);await buyer.State();await buyer.Add("bbq-chicken");var order=await buyer.Checkout();var id=Id(order);
        await buyer.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");using var admin=new Buyer(factory);await Admin(admin,factory);
        foreach(var stage in new[]{"Accepted","Preparing","Ready","OutForDelivery","Completed"}){Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/orders/{id}/status",new{status=stage},"PUT")).Status);if(stage==finalStage)break;}
        Assert.Equal(allowed,(await buyer.Send("/api/orders/"+id)).Json["canCancel"]!.GetValue<bool>());
        var result=await buyer.Send($"/api/orders/{id}/cancel",Key(),"POST");Assert.Equal(allowed?HttpStatusCode.OK:HttpStatusCode.Conflict,result.Status);
    }
}
