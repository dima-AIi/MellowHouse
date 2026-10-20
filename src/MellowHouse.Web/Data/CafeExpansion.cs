using System.Text.Json;
using System.Text.RegularExpressions;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Data;

public sealed record SampleStaff(Guid Id,string Name,string Position,string Shift,DateTime JoinedUtc);
public sealed record SampleBusiness(string From,string To,int Orders,int Customers,SampleStaff[] Staff,string Notice);
public static class CafeExpansion
{
    public const string Marker="sample.business.v3";
    public static async Task SeedAsync(IServiceProvider services,string root,CancellationToken ct=default,bool includeSamples=true)
    {
        var db=services.GetRequiredService<ShopDbContext>();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        if(!await db.SiteSettings.AnyAsync(s=>s.Key=="catalog.expanded.v3",ct))
        {
            using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"Data","catalog-expanded.json"),ct));
            var categories=await db.Categories.Select(c=>c.Id).ToHashSetAsync(ct);
            foreach(var item in doc.RootElement.EnumerateArray())
            {
                var p=JsonSerializer.Deserialize<ProductView>(item.GetRawText(),JsonDefaults.Options)!;
                if(!categories.Contains(p.CategoryId)||await db.Products.AnyAsync(x=>x.Id==p.Id,ct))continue;
                var portion=Regex.Match(p.Portion,@"^(\d+)\s*(г|мл)$");
                db.Products.Add(new(){Id=p.Id,CategoryId=p.CategoryId,Name=p.Name,Price=p.Price,Portion=p.Portion,Description=p.Description,Ingredients=p.Ingredients,Image=p.Image,Popular=p.Popular,Recommended=p.Popular,SortOrder=100+item.GetProperty("sortOrder").GetInt32(),GroupsJson=JsonSerializer.Serialize(p.Groups,JsonDefaults.Options),AllergensJson=JsonSerializer.Serialize(p.Allergens,JsonDefaults.Options),Serving=portion.Success?decimal.Parse(portion.Groups[1].Value):null,Unit=portion.Success?portion.Groups[2].Value:""});
            }
            var setting=await db.SiteSettings.FindAsync(["site"],ct);
            if(setting is not null)
            {
                var s=JsonSerializer.Deserialize<CafeSettings>(setting.Json,JsonDefaults.Options)!;
                s.Faq=s.Faq.Select(f=>f.Question switch {
                    "Как забрать заказ?"=>new FaqItem("Можно заказать к определённому времени?","Да. При оформлении выберите «Ко времени» и укажите доступное время. Подготовим заказ к вашему приходу; следить за статусом можно в истории заказов."),
                    "Где посмотреть состав?"=>new FaqItem("Что выбрать, если я не пью обычное молоко?","В карточке напитка посмотрите доступные виды молока. Состав и аллергены указаны там же. При аллергии учитывайте возможный контакт с другими продуктами на общем оборудовании."),_=>f}).ToArray();
                setting.Json=JsonSerializer.Serialize(s,JsonDefaults.Options);
            }
            db.SiteSettings.Add(new(){Key="catalog.expanded.v3"});await db.SaveChangesAsync(ct);
        }
        await MenuCompletion.ApplyAsync(db,root,ct);
        if(!includeSamples||await db.SiteSettings.AnyAsync(x=>x.Key==Marker,ct)){await tx.CommitAsync(ct);return;}
        var now=DateTime.UtcNow;var today=now.AddHours(3).Date;var first=today.AddMonths(-3);var rng=new Random(20261020);
        string[] names=["Анна","Александр","Мария","Дмитрий","Екатерина","Михаил","Дарья","Илья","София","Артём","Елена","Андрей","Алина","Максим","Полина","Кирилл","Виктория","Никита","Ольга","Роман"];
        string[] surnames=["С.","К.","М.","П.","Л.","В."];
        var guests=Enumerable.Range(1,120).Select(i=>new Customer{LoginKey="sample:guest:"+i,Name=names[(i-1)%names.Length]+" "+surnames[(i-1)/names.Length],Contact=$"guest{i:000}@mellow.example.test",CreatedUtc=DateTime.SpecifyKind(first.AddDays(rng.Next(0,15)).AddHours(7),DateTimeKind.Utc)}).ToArray();
        db.Customers.AddRange(guests);
        var staff=new[]{("Александра К.","Управляющая","5/2, дневная"),("Михаил П.","Повар","2/2, первая смена"),("Елена С.","Повар","2/2, вторая смена"),("Артём В.","Бариста","2/2, первая смена"),("Дарья М.","Бариста","2/2, вторая смена"),("Полина Л.","Кассир","2/2, первая смена"),("Никита К.","Кассир","2/2, вторая смена"),("Илья С.","Курьер","Гибкий график")}.Select((s,i)=>new SampleStaff(Guid.NewGuid(),s.Item1,s.Item2,s.Item3,DateTime.SpecifyKind(first.AddDays(-14+i),DateTimeKind.Utc))).ToArray();
        var products=await db.Products.Include(p=>p.Category).Where(p=>p.Available&&!p.Archived&&!p.Draft&&p.Category.Visible&&!p.Category.Archived).OrderBy(p=>p.Id).ToArrayAsync(ct);
        if(products.Length==0)throw new InvalidOperationException("Catalog required for sample history");
        var samples=new List<Order>();
        var serial=0;
        for(var date=first;date<=today;date=date.AddDays(1))
        {
            var age=(date-first).Days;
            var count=date==today?6:7+age/20+rng.Next(6)+(date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday?4:0);
            for(var n=0;n<count;n++)
            {
                var created=DateTime.SpecifyKind(date.AddHours(6).AddMinutes(n*660d/count+rng.Next(15)),DateTimeKind.Utc);
                if(created.AddHours(1)>now)continue;
                var eligible=guests.Where(c=>c.CreatedUtc<=created).ToArray();if(eligible.Length==0)continue;
                var guest=eligible[rng.Next(eligible.Length)];
                var selected=products.OrderBy(_=>rng.Next()).Take(rng.Next(1,4)).ToArray();
                var lines=selected.Select(p=>{var view=p.View();var picks=view.Groups.ToDictionary(g=>g.Id,g=>g.Defaults);var calculated=Pricing.Calculate(view,picks);var qty=rng.Next(100)<12?2:1;return new OrderLineSnapshot(p.Id,p.Name,p.Image,p.Portion,calculated.Canonical,calculated.Summary,qty,calculated.UnitPrice,qty*calculated.UnitPrice);}).ToArray();
                var delivery=rng.Next(100)<24;var failed=rng.Next(100)<4;var reject=!failed&&rng.Next(100)<3;
                var method=PaymentMethods.All[rng.Next(PaymentMethods.All.Length)];var subtotal=lines.Sum(l=>l.Total);var deliveryPrice=delivery?190:0;
                var requestKey="sample-v3-"+(serial++).ToString("D5");
                var checkout=new CheckoutRequest(requestKey,delivery?"delivery":"pickup",delivery?"Учебный город":null,delivery?"Примерная улица, 1":null,null,guest.Name,"+7000000"+int.Parse(guest.LoginKey.Split(':')[^1]).ToString("D4"),"mellow-house","asap",null,"Синтетическая запись для демонстрации работы кафе",method,true);
                var o=new Order{CustomerId=guest.Id,RequestKey=requestKey,Source="Sample",CreatedUtc=created,PaidUtc=failed?null:created.AddMinutes(PaymentMethods.AtPickup(method)?25:1),Status=failed?"Failed":"Paid",Fulfillment=failed||reject?"Rejected":"Completed",Subtotal=subtotal,Delivery=deliveryPrice,Total=subtotal+deliveryPrice,PaymentMethod=method,LinesJson=JsonSerializer.Serialize(lines,JsonDefaults.Options),CheckoutJson=JsonSerializer.Serialize(checkout,JsonDefaults.Options),CostsJson=JsonSerializer.Serialize(lines.Select(l=>(int?)Math.Round(l.UnitPrice*(.27+rng.NextDouble()*.12))),JsonDefaults.Options)};
                if(date==today&&!failed&&!reject&&n>=3)o.Fulfillment=new[]{"New","Preparing","Ready"}[n-3];
                guest.LastActiveUtc=created; if(rng.Next(100)<30)guest.LastLoginUtc=created.AddMinutes(-3);
                samples.Add(o);
            }
        }
        db.Orders.AddRange(samples);await db.SaveChangesAsync(ct);
        foreach(var o in samples)
        {
            db.PaymentAttempts.Add(new(){OrderId=o.Id,AttemptKey="sample-"+o.PublicId,Result=o.PaidUtc is null?"error":"success",CreatedUtc=o.PaidUtc??o.CreatedUtc.AddMinutes(1)});
            db.OrderEvents.Add(new(){OrderId=o.Id,Status="New",Note="Пример: заказ поступил",CreatedUtc=o.CreatedUtc});
            if(o.Fulfillment!="New")db.OrderEvents.Add(new(){OrderId=o.Id,ActorId=staff[2+rng.Next(5)].Id,Status=o.Fulfillment,Note=o.Fulfillment=="Rejected"?"Пример: отменён по просьбе гостя":"Пример: статус обновлён сотрудником",CreatedUtc=o.CreatedUtc.AddMinutes(30)});
            if(o.Fulfillment=="Completed"&&rng.Next(100)<2&&o.CreatedUtc.AddHours(3)<now)
            {
                var line=OrderService.Lines(o)[0];o.Refunded=line.UnitPrice;o.Status=o.Refunded==o.Total?"Refunded":"PartiallyRefunded";
                db.OrderRefunds.Add(new(){OrderId=o.Id,ActorId=staff[0].Id,RequestKey="sample-refund-"+o.PublicId,Reason="Пример: одна позиция заменена по просьбе гостя",Amount=line.UnitPrice,LinesJson=JsonSerializer.Serialize(new[]{new RefundLine(0,1)},JsonDefaults.Options),CreatedUtc=o.CreatedUtc.AddHours(2)});
            }
        }
        var info=new SampleBusiness(first.ToString("yyyy-MM-dd"),today.ToString("yyyy-MM-dd"),samples.Count,guests.Length,staff,"Синтетический пример работы кафе за три месяца. Заказы с меткой «Пример», гости example.test и команда вымышлены; реальные записи сохранены.");
        db.SiteSettings.Add(new(){Key=Marker,Json=JsonSerializer.Serialize(info,JsonDefaults.Options)});
        db.AuditEntries.Add(new(){Action="sample.seeded",Target=Marker,Detail=$"Синтетические записи: {samples.Count} заказов, {guests.Length} гостей, {staff.Length} сотрудников; {info.From} — {info.To}"});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
