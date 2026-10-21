using System.Globalization;
using System.Text.RegularExpressions;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Features;

/// <summary>Answers personal order questions locally; no private history is sent to a model.</summary>
public sealed class AssistantHistory(ShopDbContext db)
{
    private static bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    public static bool IsQuestion(string text)=>Has(text,@"^\s*(?:а\s+)?(?:вчера|позавчера|сегодня|(?:в|во)\s+(?:понедельник|вторник|среду|четверг|пятницу|субботу|воскресенье))\s*[?!.]*$|истори[яию]|мои[хм]?\s+заказ|последн[а-я]*\s+заказ|повтор[а-я]*.*заказ|что\s+я\s+(?:ел[аи]?|пил[аи]?|заказывал[аи]?|брал[аи]?)|что\s+(?:ел[аи]?|пил[аи]?|заказывал[аи]?|брал[аи]?|было)\b|(?:как|так[а-я]*|то\s+же|повтор[а-я]*|заказ[а-я]*)\s+(?:в\s+|во\s+)?(?:прошл|последн|вчера|позавчера|сегодня|понедельник|вторник|сред|четверг|пятниц|суббот|воскресень)|(?:хочу|повтори|покажи|напомни).*\b(?:вчера|позавчера|среду|вторник|понедельник|четверг|пятницу|субботу|воскресенье|последний\s+заказ)\b");

    public static DateOnly? Day(string message,DateOnly today)
    {
        if(Has(message,"позавчера"))return today.AddDays(-2);
        if(Has(message,"вчера"))return today.AddDays(-1);
        if(Has(message,"сегодня"))return today;
        var date=Regex.Match(message,@"\b(\d{1,2}[./]\d{1,2}(?:[./]\d{4})?|\d{4}-\d{2}-\d{2})\b");
        if(date.Success)
        {
            var raw=date.Value.Replace('/','.');
            if(DateOnly.TryParseExact(raw,["d.M.yyyy","dd.MM.yyyy","yyyy-MM-dd","d.M","dd.MM"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))
                return raw.Count(c=>c=='.')==1?new DateOnly(today.Year,parsed.Month,parsed.Day):parsed;
        }
        var days=new[]{("понедельник",DayOfWeek.Monday),("вторник",DayOfWeek.Tuesday),("сред",DayOfWeek.Wednesday),("четверг",DayOfWeek.Thursday),("пятниц",DayOfWeek.Friday),("суббот",DayOfWeek.Saturday),("воскресень",DayOfWeek.Sunday)};
        foreach(var (word,day) in days)
        {
            if(!Has(message,word))continue;
            var difference=((int)today.DayOfWeek-(int)day+7)%7;
            // A past-meal weekday means the latest previous occurrence; today is explicit.
            if(Has(message,@"прошл[а-я]*\s+недел"))difference=((int)today.DayOfWeek+6)%7+7-((int)day+6)%7;
            else if(difference==0&&!Has(message,@"эт[а-я]*\s+недел"))difference=7;
            return today.AddDays(-difference);
        }
        return null;
    }

    public async Task<CafeAssistantReply> ReplyAsync(string message,string owner,Product[] available,CancellationToken ct)
    {
        if(!owner.StartsWith("u:",StringComparison.Ordinal)||!Guid.TryParse(owner[2..],out var customer))
            return new("Войдите в профиль, чтобы я нашёл ваши прошлые заказы. После входа продолжу с этим вопросом. Пока могу помочь выбрать из меню.",[],["Подобрать кофе","Что перекусить?"],RequiresLogin:true);
        var today=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));var day=Day(message,today);
        if(day is null&&Has(message,@"\b\d{1,2}[./]\d{1,2}|\b\d{4}-\d{2}-\d{2}"))return new("Не получилось разобрать дату. Напишите её как 21.10.2026, либо укажите «вчера» или день недели.",[],["Покажи мой последний заказ"]);
        // Use the same owner and archive boundary as the customer's history API.
        var query=db.Orders.AsNoTracking().Where(o=>o.CustomerId==customer&&o.Source!=DemoBusiness.ArchivedSource&&o.Fulfillment!="Rejected");
        if(day is {} selected){var start=selected.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc).AddHours(-3);var end=start.AddDays(1);query=query.Where(o=>o.CreatedUtc>=start&&o.CreatedUtc<end);}
        var orders=await query.OrderByDescending(o=>o.CreatedUtc).ThenByDescending(o=>o.Id).ToArrayAsync(ct);
        var placed=orders.Where(o=>(o.Status is "Paid" or "PartiallyRefunded" or "Refunded")||o.Fulfillment=="Completed"||o.Status=="Pending"&&(o.PaymentMethod is "cash" or "terminal")).ToArray();
        if(placed.Length==0)
        {
            var when=day is {} d?" за "+d.ToString("dd.MM.yyyy"):"";
            var pending=orders.Any(o=>o.Status=="Pending")?" Есть незавершённая онлайн-оплата — такой заказ ещё не передан кафе.":"";
            return new("В вашей истории нет оформленных заказов"+when+"."+pending+" Могу посмотреть последний заказ за другую дату или помочь выбрать что-нибудь новое.",[],["Покажи мой последний заказ","Что перекусить?"]);
        }
        var selectedOrders=day.HasValue?placed.Take(10).ToArray():placed.Take(1).ToArray();
        var rows=new List<string>();var ids=new List<string>();var unavailable=new HashSet<string>();
        foreach(var order in selectedOrders)
        {
            rows.Add($"{order.CreatedUtc.AddHours(3):dd.MM.yyyy}, заказ MH-{order.Id+1040}:");
            foreach(var line in OrderService.Lines(order))
            {
                var current=available.FirstOrDefault(p=>p.Id==line.ProductId);
                rows.Add("• "+line.Name+" — "+line.Quantity+" шт."+(line.Summary.Length>0?" ("+string.Join(", ",line.Summary)+")":""));
                if(current is null)unavailable.Add(line.Name);else ids.Add(current.Id);
            }
        }
        var offered=ids.Distinct().Take(6).ToArray();
        var text="Вот что вы заказывали в Mellow House:\n\n"+string.Join("\n",rows);
        if(placed.Length>selectedOrders.Length&&day.HasValue)text+="\nПоказаны последние 10 заказов за этот день. Все записи доступны в профиле.";
        if(unavailable.Count>0)text+="\n\nСейчас недоступны: "+string.Join(", ",unavailable)+". Их не добавляю.";
        if(offered.Length>0)text+="\n\nНиже блюда, которые доступны сейчас. Нажмите «Добавить» или «Настроить»: действуют текущие цены, прежние настройки указаны выше. Корзину пока не менял.";
        return new(text,offered,["Покажи мой последний заказ","Предложи что-нибудь другое"]);
    }
}
