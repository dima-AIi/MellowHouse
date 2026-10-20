using System.Text.Json;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Data;

/// <summary>Reduces only the former generated dataset. Never deletes customer-created orders.</summary>
public static class SampleRevision
{
    public static async Task ApplyAsync(ShopDbContext db,string dataDir,CancellationToken ct=default)
    {
        const string marker="sample.business.v4";if(await db.SiteSettings.AnyAsync(x=>x.Key==marker,ct))return;
        var today=DateTime.UtcNow.AddHours(3).Date;var first=today.AddMonths(-2);
        var samples=await db.Orders.Where(x=>x.Source=="Sample").OrderBy(x=>x.CreatedUtc).ToArrayAsync(ct);
        if(samples.Length==0)return;
        var recent=samples.Where(x=>x.CreatedUtc>=first.AddHours(-3)).ToArray();if(recent.Length==0)recent=samples;
        var keep=Enumerable.Range(0,Math.Min(50,recent.Length)).Select(i=>recent[(int)Math.Round(i*(recent.Length-1d)/Math.Max(1,Math.Min(50,recent.Length)-1))].Id).ToHashSet();
        var removed=samples.Where(x=>!keep.Contains(x.Id)).ToArray();var ids=removed.Select(x=>x.Id).ToArray();
        var attempts=await db.PaymentAttempts.Where(x=>ids.Contains(x.OrderId)).ToArrayAsync(ct);
        var events=await db.OrderEvents.Where(x=>ids.Contains(x.OrderId)).ToArrayAsync(ct);
        var refunds=await db.OrderRefunds.Where(x=>ids.Contains(x.OrderId)).ToArrayAsync(ct);
        Directory.CreateDirectory(dataDir);
        var archive=Path.Combine(dataDir,"sample-history-v3-archive.json");
        if(!File.Exists(archive))await File.WriteAllTextAsync(archive,JsonSerializer.Serialize(new{orders=removed,attempts,events,refunds},JsonDefaults.Options),ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        db.PaymentAttempts.RemoveRange(attempts);db.OrderEvents.RemoveRange(events);db.OrderRefunds.RemoveRange(refunds);db.Orders.RemoveRange(removed);await db.SaveChangesAsync(ct);
        var emptyGuests=await db.Customers.Where(x=>x.LoginKey.StartsWith("sample:")&&!db.Orders.Any(o=>o.CustomerId==x.Id)&&!db.Carts.Any(c=>c.Owner=="u:"+x.Id)).ToArrayAsync(ct);
        // Unused generated guests have no sign-in credentials or customer-created orders.
        db.Customers.RemoveRange(emptyGuests);
        var entry=await db.SiteSettings.FindAsync([CafeExpansion.Marker],ct);
        if(entry is not null){var info=JsonSerializer.Deserialize<SampleBusiness>(entry.Json,JsonDefaults.Options)!;entry.Json=JsonSerializer.Serialize(info with{From=first.ToString("yyyy-MM-dd"),Orders=keep.Count,Customers=samples.Where(x=>keep.Contains(x.Id)).Select(x=>x.CustomerId).Distinct().Count(),Notice="Учебная история: 50 заказов за два месяца. Исходный большой набор сохранён в архиве."},JsonDefaults.Options);}
        if(!await db.SiteSettings.AnyAsync(x=>x.Key==FinanceService.Key,ct))
        {
            var expenses=new List<Expense>();for(var date=first;date<=today;date=date.AddMonths(1))foreach(var item in new[]{("rent",35000,"Аренда"),("payroll",120000,"Оплата работы команды"),("utilities",6500,"Коммунальные услуги"),("marketing",2500,"Реклама")})expenses.Add(new(Guid.NewGuid(),date.ToString("yyyy-MM-dd"),item.Item1,item.Item2,item.Item3,false,"Sample"));
            db.SiteSettings.Add(new(){Key=FinanceService.Key,Json=JsonSerializer.Serialize(expenses,JsonDefaults.Options)});
        }
        db.SiteSettings.Add(new(){Key=marker,Json="{\"orders\":50}"});Audit.Log(db,Guid.Empty,"sample.revised",marker,"Прежний набор архивирован; сохранено 50 учебных заказов, пользовательские записи не менялись.");
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
