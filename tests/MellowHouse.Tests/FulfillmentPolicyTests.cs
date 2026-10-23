using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using MellowHouse.Web.Features;
using Xunit;

namespace MellowHouse.Tests;

public sealed class FulfillmentPolicyTests
{
    static CheckoutRequest Request(string method="delivery",string timing="scheduled",string? time="2026-10-24T12:00",string city="Москва")=>
        new(Guid.NewGuid().ToString(),method,city,"Тестовая, 10","12","Тест","+79990000000","demo-house",timing,time,"","cash",true);
    static readonly DateTime Noon=new(2026,10,23,12,0,0);

    [Fact] public void RejectsClosedHours_TooSoon_PastAndTooDistant()
    {
        foreach(var time in new[]{"2026-10-24T03:00","2026-10-24T22:00","2026-10-23T12:29","2026-10-22T12:00","2026-10-31T12:00"})
            Assert.Equal("scheduledLocal",Assert.Throws<ShopException>(()=>FulfillmentPolicy.Validate(Request(time:time),new(),Noon)).Field);
        FulfillmentPolicy.Validate(Request(time:"2026-10-23T12:30"),new(),Noon);
        FulfillmentPolicy.Validate(Request(time:"2026-10-24T08:00"),new(),Noon);
    }
    [Fact] public void ScheduledOrdersRespectWeekendsAndClosedDates()
    {
        Assert.Throws<ShopException>(()=>FulfillmentPolicy.Validate(Request(),new(){ClosedWeekdays=[6]},Noon));
        Assert.Throws<ShopException>(()=>FulfillmentPolicy.Validate(Request(),new(){ClosedDates=["2026-10-24"]},Noon));
        FulfillmentPolicy.Validate(Request(time:"2026-10-25T12:00"),new(){ClosedDates=["2026-10-24"]},Noon);
    }
    [Fact] public void ImmediateOrdersNeedOpenCafeAndPreparationTime()
    {
        foreach(var hour in new[]{7,22,23})Assert.Throws<ShopException>(()=>FulfillmentPolicy.Validate(Request(timing:"asap"),new(),Noon.Date.AddHours(hour)));
        Assert.Throws<ShopException>(()=>FulfillmentPolicy.Validate(Request(timing:"asap"),new(),Noon.Date.AddHours(21).AddMinutes(45)));
        FulfillmentPolicy.Validate(Request(timing:"asap"),new(),Noon);
    }
    [Fact] public void DeliveryUsesConfiguredCities_WhilePickupIgnoresCity()
    {
        Assert.Equal("city",Assert.Throws<ShopException>(()=>FulfillmentPolicy.Validate(Request(city:"Владивосток"),new(),Noon)).Field);
        FulfillmentPolicy.Validate(Request(city:" г. москва "),new(),Noon);
        FulfillmentPolicy.Validate(Request(method:"pickup",city:"Владивосток"),new(),Noon);
        FulfillmentPolicy.Validate(Request(city:"Казань"),new(){DeliveryCities=["Казань"]},Noon);
    }
    [Fact] public void InvalidSettingsCannotDisableRestrictionsSilently()
    {
        Assert.Throws<ShopException>(()=>FulfillmentPolicy.ValidateSettings(new(){OpeningTime="22:00",ClosingTime="08:00"}));
        Assert.Throws<ShopException>(()=>FulfillmentPolicy.ValidateSettings(new(){DeliveryCities=[]}));
        Assert.Throws<ShopException>(()=>FulfillmentPolicy.ValidateSettings(new(){ClosedWeekdays=[0,1,2,3,4,5,6]}));
        Assert.Throws<ShopException>(()=>FulfillmentPolicy.ValidateSettings(new(){ClosedDates=["2026-02-30"]}));
    }
    [Fact] public async Task DirectOrderApiCannotBypassCityAndSchedule()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("vanilla-raf");
        var body=JsonSerializer.SerializeToNode(Buyer.Details(payment:"cash"))!.AsObject();body["city"]="Владивосток";
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/orders",body,"POST")).Status);
        body["city"]="Москва";body["timing"]="scheduled";body["scheduledLocal"]=DateTime.UtcNow.Date.AddDays(1).AddHours(3).ToString("yyyy-MM-dd'T'HH:mm");
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/orders",body,"POST")).Status);
        Assert.Empty((await b.Send("/api/orders")).Json.AsArray());
        body["scheduledLocal"]=DateTime.UtcNow.Date.AddDays(1).AddHours(12).ToString("yyyy-MM-dd'T'HH:mm");
        Assert.Equal(HttpStatusCode.OK,(await b.Send("/api/orders",body,"POST")).Status);
    }
}
