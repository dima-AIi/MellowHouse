using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Data;

public sealed record CounterSaleLine(string ProductId,string Name,int Quantity,int UnitPrice,int UnitCost);
public sealed record CounterDay(string Date,int Receipts,CounterSaleLine[] Lines);

/// <summary>Explicit, one-time demo refresh. Never runs implicitly on an ordinary deployment.</summary>
public static class DemoBusiness
{
    public const string Marker="demo.business.v7";
    public const string CounterKey="finance.counter-sales.v7";
    public const string ArchivedSource="ArchivedDemo";
    public const string ArchiveReason="Архив предыдущей демонстрации";
    public static Guid StableId(string value)=>new(MD5.HashData(Encoding.UTF8.GetBytes("mellow-demo-v7:"+value)));
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,JsonDefaults.Options);

    public static async Task<CounterDay[]> CounterAsync(ShopDbContext db,CancellationToken ct=default)=>
        JsonSerializer.Deserialize<CounterDay[]>((await db.SiteSettings.FindAsync([CounterKey],ct))?.Json??"[]",JsonDefaults.Options)??[];

    public static async Task RefreshAsync(ShopDbContext db,string backupDirectory,CancellationToken ct=default)
    {
        if(await db.SiteSettings.AnyAsync(x=>x.Key==Marker,ct))return;
        var now=DateTime.UtcNow;var today=now.AddHours(3).Date;var first=today.AddMonths(-2);var last=today.AddDays(-1);
        // Local and PostgreSQL snapshots are written on the operator's machine, outside Git/publish.
        Directory.CreateDirectory(backupDirectory);
        var backup=Path.Combine(backupDirectory,"before-demo-v7-"+now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]+".json");
        var snapshot=new {
            provider=db.Database.ProviderName,createdUtc=now,
            categories=await db.Categories.AsNoTracking().ToArrayAsync(ct),products=await db.Products.AsNoTracking().ToArrayAsync(ct),
            customers=await db.Customers.AsNoTracking().ToArrayAsync(ct),orders=await db.Orders.AsNoTracking().ToArrayAsync(ct),
            attempts=await db.PaymentAttempts.AsNoTracking().ToArrayAsync(ct),events=await db.OrderEvents.AsNoTracking().ToArrayAsync(ct),refunds=await db.OrderRefunds.AsNoTracking().ToArrayAsync(ct),
            settings=await db.SiteSettings.AsNoTracking().ToArrayAsync(ct),carts=await db.Carts.AsNoTracking().ToArrayAsync(ct),lines=await db.CartLines.AsNoTracking().ToArrayAsync(ct),
            favorites=await db.Favorites.AsNoTracking().ToArrayAsync(ct),drafts=await db.CheckoutDrafts.AsNoTracking().ToArrayAsync(ct),keys=await db.DataProtectionKeys.AsNoTracking().ToArrayAsync(ct),
            resets=await db.PasswordResets.AsNoTracking().ToArrayAsync(ct),challenges=await db.AuthChallenges.AsNoTracking().ToArrayAsync(ct),audit=await db.AuditEntries.AsNoTracking().ToArrayAsync(ct)
        };
        await File.WriteAllTextAsync(backup,JsonSerializer.Serialize(snapshot,new JsonSerializerOptions(JsonDefaults.Options){ReferenceHandler=ReferenceHandler.IgnoreCycles}),ct);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var products=await db.Products.Include(p=>p.Category).Where(p=>!p.Archived&&!p.Draft&&p.Available&&!p.Category.Archived&&p.Category.Visible).OrderBy(p=>p.Id).ToArrayAsync(ct);
        if(products.Length<10)throw new InvalidOperationException("Load the complete catalog before refreshing demo history.");
        var assignedCosts=new List<string>();
        foreach(var p in products)
        {
            var ratio=p.CategoryId switch{"coffee"=>.24,"tea"=>.22,"cold"=>.28,"shakes"=>.32,"hot"=>.39,"salads"=>.37,"pizza"=>.34,_=>.33};
            if(p.Cost is null){p.Cost=(int)Math.Round(p.Price*ratio);assignedCosts.Add(p.Id);}
            var groups=p.View().Groups.Select(g=>g with{Options=g.Options.Select(o=>o.Cost is null?o with{Cost=(int)Math.Round(Math.Max(0,o.Price)*ratio)}:o).ToArray()}).ToArray();
            p.GroupsJson=Json(groups);
        }
        var oldOrders=await db.Orders.Where(o=>o.Source!=ArchivedSource).ToArrayAsync(ct);
        var archived=oldOrders.Where(o=>o.Source=="Sample"||MissingCost(o)).ToArray();
        foreach(var order in archived){order.Source=ArchivedSource;order.Revision++;}
        var oldGuests=await db.Customers.Where(c=>c.LoginKey.StartsWith("sample:")).ToArrayAsync(ct);
        foreach(var guest in oldGuests.Where(g=>!oldOrders.Any(o=>o.CustomerId==g.Id&&o.Source!=ArchivedSource)))
        {guest.Blocked=true;guest.BlockReason=ArchiveReason;}

        string[] names=["Анна","Александр","Мария","Дмитрий","Екатерина","Михаил","Дарья","Илья","София","Артём","Елена","Андрей","Алина","Максим","Полина","Кирилл","Виктория","Никита","Ольга","Роман","Валерия","Павел"];
        var guests=new List<Customer>();
        for(var i=0;i<44;i++)
        {
            var key="sample:v7:guest:"+i.ToString("D2");var guest=await db.Customers.SingleOrDefaultAsync(c=>c.LoginKey==key,ct);
            if(guest is null){guest=new(){Id=StableId(key),LoginKey=key,Name=names[i%names.Length]+" "+new[]{"С.","К.","М.","П."}[i/names.Length],Contact=$"guest{i+1:00}@mellow.example.test",CreatedUtc=Utc(first.AddDays(-3).AddHours(9))};db.Customers.Add(guest);}
            guests.Add(guest);
        }
        var staff=new[]{("Александра К.","Управляющая","5/2"),("Михаил П.","Повар","2/2, первая смена"),("Елена С.","Повар","2/2, вторая смена"),("Артём В.","Бариста","2/2, первая смена"),("Дарья М.","Бариста","2/2, вторая смена"),("Полина Л.","Кассир","2/2, первая смена"),("Никита К.","Кассир","2/2, вторая смена"),("Илья С.","Курьер","Частичная занятость")}
            .Select((s,i)=>new SampleStaff(StableId("staff:"+i),s.Item1,s.Item2,s.Item3,Utc(first.AddDays(-10+i)))).ToArray();
        var random=new Random(20261021);var drinks=products.Where(p=>new[]{"coffee","tea","cold","shakes"}.Contains(p.CategoryId)).ToArray();var foods=products.Except(drinks).Where(p=>p.CategoryId!="desserts").ToArray();var desserts=products.Where(p=>p.CategoryId=="desserts").ToArray();
        Product Pick(Product[] pool)=>pool[random.Next(pool.Length)];
        OrderLineSnapshot Line(Product p,int quantity=1)
        {var v=p.View();var priced=Pricing.Calculate(v,v.Groups.ToDictionary(g=>g.Id,g=>g.Defaults));return new(p.Id,p.Name,p.Image,p.Portion,priced.Canonical,priced.Summary,quantity,priced.UnitPrice,priced.UnitPrice*quantity);}
        int Cost(Product p)=>p.Cost!.Value+p.View().Groups.Sum(g=>g.Options.Where(o=>g.Defaults.Contains(o.Id)).Sum(o=>o.Cost??0));
        var newOrders=new List<Order>();var days=(last-first).Days+1;
        for(var i=0;i<50;i++)
        {
            var ago=i<18?31+(int)Math.Round((17-i)*(days-32d)/17):i<38?8+(37-i)*22/19:1+(49-i)*6/11;
            var created=Utc(today.AddDays(-ago).AddHours(9+random.Next(12)).AddMinutes(random.Next(60)));
            var selected=new[]{Pick(drinks),Pick(foods)}.Concat(i%3==0?new[]{Pick(desserts)}:Array.Empty<Product>()).ToArray();
            var lines=selected.Select(p=>Line(p)).ToArray();var rejected=i is 9 or 31;var delivery=i%4==0;var guest=guests[i%guests.Count];
            var checkout=new CheckoutRequest("demo-v7-"+i,delivery?"delivery":"pickup",delivery?"Москва":null,delivery?"Примерная улица, 12":null,delivery?"7":null,guest.Name,"+7000000"+(1000+i).ToString(),"mellow-house","asap",null,"","card",true);
            var order=new Order{PublicId=StableId("order:"+i),CustomerId=guest.Id,RequestKey=checkout.RequestKey,Source="Sample",CreatedUtc=created,PaidUtc=rejected?null:created.AddMinutes(1),Status=rejected?"Failed":"Paid",Fulfillment=rejected?"Rejected":"Completed",PaymentMethod="card",Subtotal=lines.Sum(l=>l.Total),Delivery=delivery?190:0,LinesJson=Json(lines),CostsJson=Json(selected.Select(p=>(int?)Cost(p)).ToArray()),CheckoutJson=Json(checkout)};
            order.Total=order.Subtotal+order.Delivery;db.Orders.Add(order);newOrders.Add(order);guest.LastActiveUtc=created;
        }
        await db.SaveChangesAsync(ct);
        foreach(var order in newOrders)
        {
            db.PaymentAttempts.Add(new(){Id=StableId("payment:"+order.RequestKey),OrderId=order.Id,AttemptKey=order.RequestKey,Result=order.PaidUtc.HasValue?"success":"error",CreatedUtc=order.CreatedUtc.AddMinutes(1)});
            var stages=order.Fulfillment=="Rejected"?new[]{"New","Rejected"}:new[]{"New","Accepted","Preparing","Ready","Completed"};
            foreach(var stage in stages.Select((s,i)=>(s,i)))
                db.OrderEvents.Add(new(){OrderId=order.Id,ActorId=stage.i==0?null:staff[3].Id,Status=stage.s,Note=stage.i==0?"Заказ поступил с сайта":"Статус обновлён сотрудником",CreatedUtc=order.CreatedUtc.AddMinutes(stage.i*6)});
        }
        var returned=newOrders[10];var firstLine=OrderService.Lines(returned)[0];returned.Refunded=firstLine.UnitPrice;returned.Status="PartiallyRefunded";
        db.OrderRefunds.Add(new(){Id=StableId("refund:10"),OrderId=returned.Id,ActorId=staff[0].Id,RequestKey="demo-v7-refund-10",Reason="Позиция отменена по просьбе гостя",Amount=firstLine.UnitPrice,LinesJson=Json(new[]{new RefundLine(0,1)}),CreatedUtc=returned.CreatedUtc.AddMinutes(35)});

        var counter=new List<CounterDay>();
        for(var day=first;day<=last;day=day.AddDays(1))
        {
            var weekend=day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;var receipts=55+random.Next(18)+(weekend?10:0);var quantities=new Dictionary<string,int>();
            void Sell(Product p)=>quantities[p.Id]=quantities.GetValueOrDefault(p.Id)+1;
            for(var receipt=0;receipt<receipts;receipt++){Sell(Pick(drinks));if(random.Next(100)<86)Sell(Pick(foods));if(random.Next(100)<28)Sell(Pick(desserts));}
            counter.Add(new(day.ToString("yyyy-MM-dd"),receipts,quantities.OrderBy(q=>q.Key).Select(q=>{var p=products.Single(x=>x.Id==q.Key);return new CounterSaleLine(p.Id,p.Name,q.Value,Line(p).UnitPrice,Cost(p));}).ToArray()));
        }
        var expenses=JsonSerializer.Deserialize<Expense[]>((await db.SiteSettings.FindAsync([FinanceService.Key],ct))?.Json??"[]",JsonDefaults.Options)??[];
        var revised=expenses.Select(e=>e.Source=="Sample"?e with{Archived=true}:e).ToList();
        var budgets=new[]{("rent",130000,"Аренда"),("payroll",650000,"Работа команды"),("utilities",25000,"Коммунальные услуги"),("marketing",20000,"Реклама"),("other",30000,"Расходные материалы и уборка")};
        for(var day=first;day<=last;day=day.AddDays(1))foreach(var (category,budget,note) in budgets)
        {
            var monthDays=DateTime.DaysInMonth(day.Year,day.Month);var daily=budget/monthDays;if(day.Day==monthDays)daily+=budget%monthDays;
            revised.Add(new(StableId("expense:"+day.ToString("yyyy-MM-dd")+category),day.ToString("yyyy-MM-dd"),category,daily,note+" — начисление за день",false,"Sample"));
        }
        await SetAsync(db,FinanceService.Key,revised,ct);await SetAsync(db,CounterKey,counter,ct);
        await SetAsync(db,CafeExpansion.Marker,new SampleBusiness(first.ToString("yyyy-MM-dd"),last.ToString("yyyy-MM-dd"),50,44,staff,"Демонстрация: 50 онлайн-заказов и дневные сводки продаж в зале за два месяца. Люди, продажи и себестоимость примеров вымышлены."),ct);
        await SetAsync(db,Marker,new{version=7,from=first.ToString("yyyy-MM-dd"),to=last.ToString("yyyy-MM-dd"),orders=50,customers=44,counterDays=counter.Count,assignedCosts,archivedOrders=archived.Length,backup=Path.GetFileName(backup),sample=true},ct);
        Audit.Log(db,Guid.Empty,"demo.refreshed",Marker,$"Архивировано {archived.Length}; 50 новых примеров, {counter.Count} дневных сводок, затраты по дням. Резервная копия: {Path.GetFileName(backup)}");
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
    }
    static DateTime Utc(DateTime local)=>DateTime.SpecifyKind(local.AddHours(-3),DateTimeKind.Utc);
    static bool MissingCost(Order order)
    {var lines=OrderService.Lines(order);var costs=OrderService.Costs(order);return lines.Length>0&&(costs.Length<lines.Length||costs.Take(lines.Length).Any(c=>c is null));}
    static async Task SetAsync<T>(ShopDbContext db,string key,T value,CancellationToken ct)
    {var row=await db.SiteSettings.FindAsync([key],ct);if(row is null){row=new(){Key=key};db.SiteSettings.Add(row);}row.Json=Json(value);}
}
