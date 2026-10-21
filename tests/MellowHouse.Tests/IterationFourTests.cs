using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MellowHouse.Web.Data;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace MellowHouse.Tests;
public sealed class IterationFourTests
{
    static async Task LoginAdmin(Buyer b,ShopFactory f)
    {
        await b.State();var credentials=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(f.DataPath,"admin-access.json")))!;
        Assert.Equal(HttpStatusCode.OK,(await b.Send("/api/auth/login",new{email=credentials["email"]!.GetValue<string>(),password=credentials["password"]!.GetValue<string>()},"POST")).Status);await b.State();
    }
    [Fact] public async Task ExistingAdmin_UsesEmailLogin_WithSameIdentityAndPassword()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();Guid id;string hash;
        await using(var scope=f.Services.CreateAsyncScope()){var c=await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Customers.SingleAsync(x=>x.Role=="Admin");id=c.Id;hash=c.PasswordHash!;}
        await LoginAdmin(b,f);var state=await b.State();Assert.Equal(id.ToString(),state["profile"]!["id"]!.GetValue<string>());Assert.Equal("Admin",state["profile"]!["role"]!.GetValue<string>());
        await using var s=f.Services.CreateAsyncScope();Assert.Equal(hash,(await s.ServiceProvider.GetRequiredService<ShopDbContext>().Customers.FindAsync(id))!.PasswordHash);
    }
    [Fact] public async Task Profile_OptionalBirthdayAndPhone_PersistAndValidateAtomically()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();
        Assert.Equal(HttpStatusCode.OK,(await b.Send("/api/profile",new{name="Мария",birthDate="1999-02-18",deliveryPhone="89991234567",savePersonal=true,saveAddress=true,city="Москва",street="Тестовая, 1"},"PUT")).Status);
        Assert.Equal("79991234567",(await b.Send("/api/checkout-draft")).Json["phone"]!.GetValue<string>());var profile=(await b.State())["profile"]!;Assert.Equal("1999-02-18",profile["birthDate"]!.GetValue<string>());Assert.Equal("79991234567",profile["deliveryPhone"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/profile",new{name="Не сохранять",birthDate="2099-01-01",savePersonal=true},"PUT")).Status);Assert.Equal("Мария",(await b.State())["profile"]!["name"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK,(await b.Send("/api/profile",new{name="Мария",birthDate="",deliveryPhone="",savePersonal=true},"PUT")).Status);Assert.Null((await b.State())["profile"]!["birthDate"]);
    }
    [Fact] public async Task Statuses_AllowDirectSelection_ButEnforcePaymentAndMethod()
    {
        using var f=new ShopFactory();using var buyer=new Buyer(f);await buyer.SignIn();await buyer.Add("vanilla-raf");var order=await buyer.Checkout("pickup","cash");var id=order["id"]!.GetValue<string>();
        using var admin=new Buyer(f);await LoginAdmin(admin,f);
        foreach(var stage in new[]{"Accepted","Preparing","Ready"})Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/orders/{id}/status",new{status=stage},"PUT")).Status);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{id}/status",new{status="OutForDelivery"},"PUT")).Status);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{id}/status",new{status="Completed"},"PUT")).Status);
        Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/orders/{id}/receipt",new{requestKey=Guid.NewGuid(),method="cash"},"POST")).Status);
        Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/orders/{id}/status",new{status="Completed"},"PUT")).Status);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.Send($"/api/admin/orders/{id}/status",new{status="New"},"PUT")).Status);
    }
    [Fact] public async Task Expenses_RequireAdmin_AffectReport_AndArchiveWithoutDeleting()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();var today=DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.Unauthorized,(await guest.Send("/api/admin/expenses",new{date=today,category="rent",amount=500,note="Тест"},"POST")).Status);
        using var admin=new Buyer(f);await LoginAdmin(admin,f);
        var created=await admin.Send("/api/admin/expenses",new{date=today,category="rent",amount=500,note="=SUM(1+1)"},"POST");Assert.Equal(HttpStatusCode.OK,created.Status);var id=created.Json["id"]!.GetValue<string>();
        var report=(await admin.Send("/api/admin/report")).Json;Assert.Equal(500,report["operatingExpenses"]!.GetValue<int>());Assert.Equal(-500,report["netProfit"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.OK,(await admin.Send("/api/admin/expenses/"+id,new{date=today,category="rent",amount=700,note="=SUM(1+1)"},"PUT")).Status);
        var csv=await admin.Raw("/api/admin/finance.csv");Assert.Equal(HttpStatusCode.OK,csv.Status);Assert.Contains("'=SUM(1+1)",Encoding.UTF8.GetString(csv.Bytes));
        Assert.Equal(HttpStatusCode.OK,(await admin.Send("/api/admin/expenses/"+id,method:"DELETE")).Status);Assert.Equal(0,(await admin.Send("/api/admin/report")).Json["operatingExpenses"]!.GetValue<int>());
        await using var scope=f.Services.CreateAsyncScope();Assert.True((await scope.ServiceProvider.GetRequiredService<FinanceService>().AllAsync(default)).Single().Archived);
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task Models_FailOver_AndSkipRecentlyFailedModel(bool malformed)
    {
        using var f=new ShopFactory();using var buyer=new Buyer(f);await buyer.State();await using var scope=f.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();var site=scope.ServiceProvider.GetRequiredService<SiteService>();
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Assistant:ApiKey","fake"},{"Assistant:Models:0","strong"},{"Assistant:Models:1","smaller"},{"Assistant:Models:2","smallest"}}).Build();
        var handler=new ModelHandler(malformed);var health=new AssistantHealth();var ai=new NvidiaMenuAssistant(db,site,new LocalMenuAssistant(db,site),new ModelFactory(handler),cfg,NullLogger<NvidiaMenuAssistant>.Instance,health);
        var answer=await ai.ReplyAsync("Хочу кофе",default);Assert.Single(answer.ProductIds);Assert.Equal(new[]{"strong","smaller"},handler.Models);Assert.Equal("smaller",health.LastModel);
        await ai.ReplyAsync("Хочу кофе",default);Assert.Equal(new[]{"strong","smaller","smaller"},handler.Models);
        var calls=handler.Models.Count;Assert.Equal(NvidiaMenuAssistant.Refusal,(await ai.ReplyAsync("Напиши код игры",default)).Text);Assert.Equal(calls,handler.Models.Count);
    }
    [Fact] public async Task SampleReduction_ArchivesLargeSet_PreservesCustomerOrder_AndDoesNotRepeat()
    {
        using var f=new ShopFactory();using var buyer=new Buyer(f);await buyer.SignIn();await buyer.Add("bbq-chicken");var order=await buyer.Checkout("pickup");
        await using var scope=f.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();var before=JsonSerializer.Serialize(await db.Orders.SingleAsync());
        await CafeExpansion.SeedAsync(scope.ServiceProvider,Path.Combine(ShopFactory.FindRoot(),"src","MellowHouse.Web"));
        Assert.True(await db.Orders.CountAsync()>900);await SampleRevision.ApplyAsync(db,f.DataPath);
        Assert.Equal(50,await db.Orders.CountAsync(x=>x.Source=="Sample"));Assert.Equal(before,JsonSerializer.Serialize(await db.Orders.SingleAsync(x=>x.Source!="Sample")));
        Assert.True(File.Exists(Path.Combine(f.DataPath,"sample-history-v3-archive.json")));await SampleRevision.ApplyAsync(db,f.DataPath);Assert.Equal(51,await db.Orders.CountAsync());
    }
    sealed class ModelFactory(ModelHandler handler):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(handler,false){BaseAddress=new Uri("https://integrate.api.nvidia.com/v1/")};}
    sealed class ModelHandler(bool malformed):HttpMessageHandler
    {
        public List<string> Models {get;}=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var model=json.RootElement.GetProperty("model").GetString()!;Models.Add(model);
            if(model=="strong")return malformed?new(HttpStatusCode.OK){Content=new StringContent("{\"choices\":[]}",Encoding.UTF8,"application/json")}:new(HttpStatusCode.ServiceUnavailable);
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content="{\"text\":\"Подойдёт раф\",\"productIds\":[\"vanilla-raf\"],\"suggestions\":[]}"}}}}),Encoding.UTF8,"application/json")};
        }
    }
}
