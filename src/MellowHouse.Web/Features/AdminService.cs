using System.Globalization;
using System.Text;
using System.Text.Json;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Features;

public sealed record BlockRequest(bool Blocked,string? Reason);
public sealed class AdminService(ShopDbContext db,OrderService orders,IdentityService identity,FinanceService finance)
{
    public async Task<object[]> ClientsAsync(CancellationToken ct)
    {
        var counts=await db.Orders.Where(x=>x.Source!=DemoBusiness.ArchivedSource).GroupBy(x=>x.CustomerId).Select(x=>new{x.Key,Count=x.Count()}).ToDictionaryAsync(x=>x.Key,x=>x.Count,ct);
        return(await db.Customers.OrderByDescending(x=>x.CreatedUtc).ToArrayAsync(ct)).Where(c=>!(c.Blocked&&c.BlockReason==DemoBusiness.ArchiveReason&&c.LoginKey.StartsWith("sample:"))).Select(c=>(object)new{c.Id,c.Name,c.Contact,sample=c.LoginKey.StartsWith("sample:"),c.EmailNormalized,c.Role,c.Blocked,c.BlockReason,c.CreatedUtc,c.LastLoginUtc,c.LastActiveUtc,active=!c.LoginKey.StartsWith("sample:")&&c.LastActiveUtc>DateTime.UtcNow.AddMinutes(-5),authMethod=c.LoginKey.StartsWith("sample:")?"sample":c.EmailNormalized is not null?"email":c.Role=="Admin"?"admin":"phone",hasPassword=c.PasswordHash is not null,orders=counts.GetValueOrDefault(c.Id)}).ToArray();
    }
    public async Task<object> ClientAsync(Guid id,CancellationToken ct)
    {
        var clients=await ClientsAsync(ct);var c=await db.Customers.FindAsync([id],ct)??throw new ShopException("Профиль не найден.",404);
        return new {profile=new {c.Id,c.Name,c.Contact,sample=c.LoginKey.StartsWith("sample:"),c.EmailNormalized,c.Role,c.Blocked,c.BlockReason,c.CreatedUtc,c.LastLoginUtc,c.LastActiveUtc,hasPassword=c.PasswordHash is not null},orders=await orders.HistoryAsync(id,ct)};
    }
    public async Task BlockAsync(Guid id,BlockRequest r,Guid actor,CancellationToken ct)
    {
        var c=await db.Customers.FindAsync([id],ct)??throw new ShopException("Профиль не найден.",404);
        if(r.Blocked&&c.Role=="Admin"&&await db.Customers.CountAsync(x=>x.Role=="Admin"&&!x.Blocked,ct)<=1)throw new ShopException("Нельзя заблокировать единственного администратора.");
        if(c.Blocked==r.Blocked)return;var reason=r.Blocked?Inputs.Required(r.Reason,"причину блокировки",300):"";
        c.Blocked=r.Blocked;c.BlockReason=reason;c.SessionVersion++;Audit.Log(db,actor,r.Blocked?"customer.blocked":"customer.unblocked",id.ToString(),reason);await db.SaveChangesAsync(ct);
    }
    public async Task<string> ResetAsync(Guid id,Guid actor,CancellationToken ct)=>await identity.IssueResetAsync(await db.Customers.FindAsync([id],ct)??throw new ShopException("Профиль не найден.",404),actor,ct);
    public async Task<OrderView[]> OrdersAsync(CancellationToken ct)=>(await db.Orders.Where(x=>x.Source!=DemoBusiness.ArchivedSource).OrderByDescending(x=>x.CreatedUtc).ThenByDescending(x=>x.Id).Take(2000).ToArrayAsync(ct)).Select(x=>OrderService.View(x)).ToArray();
    public async Task<object> ReportAsync(string? from,string? to,CancellationToken ct)
    {
        var today=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        if(!DateOnly.TryParseExact(from??today.ToString("yyyy-MM-dd"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var first)||!DateOnly.TryParseExact(to??today.ToString("yyyy-MM-dd"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var last)||last<first||last.DayNumber-first.DayNumber>3660)throw new ShopException("Проверьте период отчёта (не более 10 лет).");
        var start=DateTime.SpecifyKind(first.ToDateTime(TimeOnly.MinValue).AddHours(-3),DateTimeKind.Utc);var end=DateTime.SpecifyKind(last.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-3),DateTimeKind.Utc);
        var created=await db.Orders.Where(x=>x.Source!=DemoBusiness.ArchivedSource&&x.CreatedUtc>=start&&x.CreatedUtc<end).ToArrayAsync(ct);
        var paid=await db.Orders.Where(x=>x.Source!=DemoBusiness.ArchivedSource&&x.PaidUtc>=start&&x.PaidUtc<end).ToArrayAsync(ct);
        var refunds=await db.OrderRefunds.Where(x=>x.CreatedUtc>=start&&x.CreatedUtc<end&&db.Orders.Any(o=>o.Id==x.OrderId&&o.Source!=DemoBusiness.ArchivedSource)).ToArrayAsync(ct);
        var refundOrders=await db.Orders.Where(x=>refunds.Select(r=>r.OrderId).Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        var rows=new Dictionary<string,ReportRow>();
        void Add(Order o,int index,int quantity)
        {
            var line=OrderService.Lines(o)[index];if(!rows.TryGetValue(line.ProductId,out var row)){row=new(){Id=line.ProductId,Name=line.Name};rows.Add(line.ProductId,row);}
            var costs=OrderService.Costs(o);var cost=index<costs.Length?costs[index]:null;
            row.Quantity+=quantity;row.Revenue+=(long)quantity*line.UnitPrice;if(cost.HasValue)row.KnownCost+=(long)quantity*cost.Value;else row.AddUnknown(o.Id,index,quantity);
        }
        foreach(var o in paid){var lines=OrderService.Lines(o);for(var i=0;i<lines.Length;i++)Add(o,i,lines[i].Quantity);}
        foreach(var r in refunds){var o=refundOrders[r.OrderId];foreach(var line in JsonSerializer.Deserialize<RefundLine[]>(r.LinesJson,JsonDefaults.Options)??[])Add(o,line.Index,-line.Quantity);}
        var counter=(await DemoBusiness.CounterAsync(db,ct)).Where(d=>string.CompareOrdinal(d.Date,first.ToString("yyyy-MM-dd"))>=0&&string.CompareOrdinal(d.Date,last.ToString("yyyy-MM-dd"))<=0).ToArray();
        foreach(var line in counter.SelectMany(d=>d.Lines))
        {
            if(!rows.TryGetValue(line.ProductId,out var row)){row=new(){Id=line.ProductId,Name=line.Name};rows.Add(line.ProductId,row);}
            row.Quantity+=line.Quantity;row.Revenue+=(long)line.Quantity*line.UnitPrice;row.KnownCost+=(long)line.Quantity*line.UnitCost;
        }
        var counterRevenue=counter.Sum(d=>d.Lines.Sum(l=>(long)l.Quantity*l.UnitPrice));var counterReceipts=counter.Sum(d=>d.Receipts);
        var webGross=paid.Sum(x=>(long)x.Total);var gross=webGross+counterRevenue;var collected=gross-refunds.Sum(x=>(long)x.Amount);
        var productRevenue=paid.Sum(x=>(long)x.Subtotal)-refunds.Sum(x=>(long)x.Amount-x.Delivery)+counterRevenue;var missing=rows.Values.Any(x=>x.MissingCost);var costTotal=rows.Values.Sum(x=>x.KnownCost);
        var expenses=await finance.PeriodAsync(first.ToString("yyyy-MM-dd"),last.ToString("yyyy-MM-dd"),ct);
        var operatingExpenses=expenses.Sum(x=>(long)x.Amount);
        var submitted=created.Where(x=>x.PaidUtc is not null||PaymentMethods.AtPickup(x.PaymentMethod)).ToArray();
        return new {counterRevenue,counterReceipts,webRevenue=webGross-refunds.Sum(x=>(long)x.Amount),paidReceipts=paid.Length+counterReceipts,receipts=created.Length+counterReceipts,expenseCategories=expenses.GroupBy(e=>e.Category).Select(g=>new{category=g.Key,total=g.Sum(e=>(long)e.Amount),count=g.Count()}).ToArray(),operatingExpenses,expenses,totalKnownExpenses=costTotal+operatingExpenses,netProfit=missing?(long?)null:collected-costTotal-operatingExpenses,provisionalResult=collected-costTotal-operatingExpenses,from=first.ToString("yyyy-MM-dd"),to=last.ToString("yyyy-MM-dd"),sampleOrders=created.Count(x=>x.Source=="Sample"),basis=counter.Length>0?"Продажи в зале и онлайн-заказы учитываются отдельно, без повторного сложения. МСК; расходы примеров начислены по дням. Очередь показывает только онлайн-заказы.":"Оплаты и возвраты по времени операции (МСК). Очередь — заказы, созданные в выбранный период.",created=created.Length,paidOrders=paid.Length,newOrders=submitted.Count(x=>x.Fulfillment=="New"),preparing=submitted.Count(x=>new[]{"Accepted","Preparing","OutForDelivery"}.Contains(x.Fulfillment)),ready=submitted.Count(x=>x.Fulfillment=="Ready"),completed=created.Count(x=>x.Fulfillment=="Completed"),rejected=created.Count(x=>x.Fulfillment=="Rejected"),gross,refunds=refunds.Sum(x=>(long)x.Amount),collected,productRevenue,delivery=paid.Sum(x=>(long)x.Delivery)-refunds.Sum(x=>(long)x.Delivery),average=paid.Length+counterReceipts>0?(decimal?)Math.Round((decimal)gross/(paid.Length+counterReceipts),2):null,cost=missing?(long?)null:costTotal,knownCost=costTotal,missingCost=missing,margin=missing?(long?)null:productRevenue-costTotal,marginPercent=missing||productRevenue==0?(decimal?)null:Math.Round((decimal)(productRevenue-costTotal)/productRevenue*100,1),products=rows.Values.OrderByDescending(x=>x.Revenue).ToArray()};
    }
    public async Task<byte[]> CsvAsync(string? from,string? to,CancellationToken ct)
    {
        var report=JsonSerializer.SerializeToElement(await ReportAsync(from,to,ct),JsonDefaults.Options);
        string Safe(string value){if(value.Length>0&&"=+-@\t\r".Contains(value[0]))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
        var b=new StringBuilder("Период;Товар;Количество с учётом возвратов;Выручка по товарам;Себестоимость;Маржа\r\n");
        foreach(var p in report.GetProperty("products").EnumerateArray()){
            var missing=p.GetProperty("missingCost").GetBoolean();var revenue=p.GetProperty("revenue").GetInt64();var cost=p.GetProperty("knownCost").GetInt64();
            b.AppendLine(string.Join(';',Safe(report.GetProperty("from").GetString()+" — "+report.GetProperty("to").GetString()),Safe(p.GetProperty("name").GetString()!),p.GetProperty("quantity").GetInt64(),revenue,Safe(missing?"Не указана":cost.ToString(CultureInfo.InvariantCulture)),Safe(missing?"Нет данных":(revenue-cost).ToString(CultureInfo.InvariantCulture))));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(b.ToString())).ToArray();
    }
    public async Task<byte[]> FinanceCsvAsync(string? from,string? to,CancellationToken ct)
    {
        var report=JsonSerializer.SerializeToElement(await ReportAsync(from,to,ct),JsonDefaults.Options);
        static string Safe(string value)=>"\""+(value.StartsWith('=')||value.StartsWith('+')||value.StartsWith('-')||value.StartsWith('@')?"'":"")+value.Replace("\"","\"\"")+"\"";
        var b=new StringBuilder("Показатель;Сумма, ₽\r\n");
        foreach(var item in new[]{("Поступления","collected"),("Продажи в зале","counterRevenue"),("Онлайн после возвратов","webRevenue"),("Себестоимость учтённая","knownCost"),("Расходы кафе","operatingExpenses"),("Результат по учтённым затратам","provisionalResult")})b.AppendLine(Safe(item.Item1)+";"+report.GetProperty(item.Item2).GetInt64());
        b.AppendLine("Полная себестоимость;"+(!report.GetProperty("missingCost").GetBoolean()?"Да":"Нет"));b.AppendLine("Дата;Категория;Сумма;Комментарий");
        foreach(var e in report.GetProperty("expenses").EnumerateArray())b.AppendLine(string.Join(';',Safe(e.GetProperty("date").GetString()!),Safe(e.GetProperty("category").GetString()!),e.GetProperty("amount").GetInt32(),Safe(e.GetProperty("note").GetString()!)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(b.ToString())).ToArray();
    }
    private sealed class ReportRow
    {
        public string Id{get;set;}="";public string Name{get;set;}="";public long Quantity{get;set;}public long Revenue{get;set;}public long KnownCost{get;set;}private readonly Dictionary<(long,int),int> unknown=[];public bool MissingCost=>unknown.Values.Any(q=>q!=0);public void AddUnknown(long id,int index,int quantity){var key=(id,index);unknown[key]=unknown.GetValueOrDefault(key)+quantity;}
    }
}
