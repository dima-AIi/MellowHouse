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

public sealed class DemoBusinessTests
{
    static string Day(int ago)=>DateTime.UtcNow.AddHours(3).Date.AddDays(-ago).ToString("yyyy-MM-dd");
    static async Task Expand(ShopFactory f,IServiceProvider services)=>await CafeExpansion.SeedAsync(services,Path.Combine(ShopFactory.FindRoot(),"src","MellowHouse.Web"),includeSamples:false);
    static async Task<JsonNode> Report(IServiceProvider services,int days)=>JsonSerializer.SerializeToNode(await services.GetRequiredService<AdminService>().ReportAsync(Day(days-1),Day(0),default),JsonDefaults.Options)!;
    static long N(JsonNode r,string name)=>r[name]!.GetValue<long>();

    [Fact] public async Task Demo_SeparatesCounterSales_ReconcilesCostsAndRefunds_AndScalesPeriods()
    {
        using var f=new ShopFactory();using var buyer=new Buyer(f);await buyer.State();await using var scope=f.Services.CreateAsyncScope();var services=scope.ServiceProvider;var db=services.GetRequiredService<ShopDbContext>();
        await Expand(f,services);Assert.False(await db.SiteSettings.AnyAsync(s=>s.Key==DemoBusiness.Marker));
        await DemoBusiness.RefreshAsync(db,Path.Combine(f.DataPath,"backups"));
        Assert.Equal(50,await db.Orders.CountAsync(o=>o.Source=="Sample"));Assert.Equal(44,await db.Customers.CountAsync(c=>c.LoginKey.StartsWith("sample:v7:")));
        var products=await db.Products.ToArrayAsync();Assert.Equal(78,products.Length);Assert.All(products,p=>{Assert.NotNull(p.Cost);Assert.All(p.View().Groups.SelectMany(g=>g.Options),o=>Assert.NotNull(o.Cost));});
        var sample=JsonSerializer.Deserialize<SampleBusiness>((await db.SiteSettings.FindAsync(CafeExpansion.Marker))!.Json,JsonDefaults.Options)!;Assert.Equal(8,sample.Staff.Length);
        var counter=await DemoBusiness.CounterAsync(db);Assert.All(counter,d=>Assert.True(string.CompareOrdinal(d.Date,Day(0))<0));Assert.True(counter.Length>=58);
        foreach(var days in new[]{7,30,60})
        {
            var report=await Report(services,days);Assert.False(report["missingCost"]!.GetValue<bool>());Assert.True(N(report,"netProfit")>0);
            Assert.Equal(N(report,"collected"),N(report,"counterRevenue")+N(report,"webRevenue"));
            Assert.Equal(N(report,"collected"),N(report,"productRevenue")+N(report,"delivery"));
            Assert.Equal(N(report,"productRevenue"),report["products"]!.AsArray().Sum(p=>N(p!,"revenue")));
            Assert.Equal(N(report,"knownCost"),report["products"]!.AsArray().Sum(p=>N(p!,"knownCost")));
            Assert.Equal(N(report,"operatingExpenses"),report["expenseCategories"]!.AsArray().Sum(p=>N(p!,"total")));
            Assert.Equal(N(report,"collected")-N(report,"knownCost")-N(report,"operatingExpenses"),N(report,"netProfit"));
            Assert.Equal(N(report,"paidOrders")+N(report,"counterReceipts"),N(report,"paidReceipts"));
        }
        var week=await Report(services,7);var month=await Report(services,30);
        Assert.InRange((double)N(month,"totalKnownExpenses")/N(week,"totalKnownExpenses"),3.5,6);
        Assert.InRange((double)N(month,"operatingExpenses")/N(week,"operatingExpenses"),3.5,6);
        var csv=Encoding.UTF8.GetString(await services.GetRequiredService<AdminService>().FinanceCsvAsync(Day(29),Day(0),default));Assert.Contains("Продажи в зале",csv);Assert.Contains(N(month,"collected").ToString(),csv);
        await services.GetRequiredService<FinanceService>().SaveAsync(null,new(Day(1),"other",100000000,"Проверка реального убытка"),Guid.Empty,default);
        Assert.True(N(await Report(services,7),"netProfit")<0);
    }

    [Fact] public async Task Refresh_ArchivesUnknownOrders_Reversibly_PreservesAccountsCartAndCatalog_AndIsIdempotent()
    {
        using var f=new ShopFactory();using var buyer=new Buyer(f);await buyer.SignIn();await buyer.Add("vanilla-raf");var old=await buyer.Checkout("pickup");var oldId=old["id"]!.GetValue<Guid>();await buyer.Add("bbq-chicken");
        await using var scope=f.Services.CreateAsyncScope();var services=scope.ServiceProvider;var db=services.GetRequiredService<ShopDbContext>();await Expand(f,services);
        var account=await db.Customers.SingleAsync(c=>c.Role!="Admin");var hash=account.PasswordHash;var cartCount=await db.CartLines.CountAsync();
        var p=(await db.Products.FindAsync("vanilla-raf"))!;p.Name="Мой раф";p.Price=421;p.Cost=123;var image=p.Image;
        var known=new Order{CustomerId=account.Id,RequestKey="keep-known",Source="Web",Status="Paid",PaidUtc=DateTime.UtcNow.AddDays(-1),Subtotal=500,Total=500,LinesJson=JsonSerializer.Serialize(new[]{new OrderLineSnapshot(p.Id,p.Name,p.Image,p.Portion,"",[],1,500,500)},JsonDefaults.Options),CostsJson="[123]"};db.Orders.Add(known);await db.SaveChangesAsync();
        var finance=services.GetRequiredService<FinanceService>();var personalExpense=await finance.SaveAsync(null,new(Day(1),"other",50,"Сохранить мой расход"),account.Id,default);
        var backupDir=Path.Combine(f.DataPath,"backups");await DemoBusiness.RefreshAsync(db,backupDir);
        Assert.Equal(DemoBusiness.ArchivedSource,(await db.Orders.SingleAsync(o=>o.PublicId==oldId)).Source);Assert.Equal("Web",known.Source);
        Assert.Equal(hash,account.PasswordHash);Assert.False(account.Blocked);Assert.Equal(cartCount,await db.CartLines.CountAsync());Assert.Equal("Мой раф",p.Name);Assert.Equal(421,p.Price);Assert.Equal(123,p.Cost);Assert.Equal(image,p.Image);
        Assert.Contains(await finance.AllAsync(default),e=>e.Id==personalExpense.Id&&!e.Archived);
        Assert.Equal(HttpStatusCode.NotFound,(await buyer.Send("/api/orders/"+oldId)).Status);
        await Assert.ThrowsAsync<ShopException>(()=>services.GetRequiredService<OrderService>().AdminFindAsync(oldId,default));
        Assert.DoesNotContain((await buyer.Send("/api/orders")).Json.AsArray(),o=>o!["id"]!.GetValue<Guid>()==oldId);
        var backup=Assert.Single(Directory.GetFiles(backupDir));var saved=JsonNode.Parse(await File.ReadAllTextAsync(backup))!;Assert.Contains(saved["orders"]!.AsArray(),o=>o!["publicId"]!.GetValue<Guid>()==oldId);
        var total=await db.Orders.CountAsync();p.Price=432;await db.SaveChangesAsync();await DemoBusiness.RefreshAsync(db,backupDir);Assert.Equal(total,await db.Orders.CountAsync());Assert.Single(Directory.GetFiles(backupDir));Assert.Equal(432,p.Price);
        // Later genuinely incomplete orders must still surface incomplete accounting, even without the old banner.
        db.Orders.Add(new(){CustomerId=account.Id,RequestKey="new-unknown",PaidUtc=DateTime.UtcNow,LinesJson=known.LinesJson,CostsJson="[]",Subtotal=500,Total=500});await db.SaveChangesAsync();
        var report=await Report(services,7);Assert.True(report["missingCost"]!.GetValue<bool>());Assert.Null(report["netProfit"]);
    }
}
