using System.Text.RegularExpressions;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Features;
public sealed record AssistantRequest(string Message,string[]? RecentQuestions=null,string[]? LastProductIds=null,string? RequestId=null);
public sealed record AssistantAnswer(string Text,string[] ProductIds,string[] Suggestions);
public interface IMenuAssistant{Task<AssistantAnswer> ReplyAsync(string message,CancellationToken ct,string[]? recentQuestions=null);}
public sealed class LocalMenuAssistant(ShopDbContext db,SiteService site):IMenuAssistant
{
    public async Task<AssistantAnswer> ReplyAsync(string message,CancellationToken ct,string[]? recentQuestions=null)
    {
        message=Inputs.Required(message,"вопрос",500);
        var previous=NvidiaMenuAssistant.Recent(recentQuestions).LastOrDefault();
        if(previous is not null&&NvidiaMenuAssistant.Followup(message))message=previous+" "+message;
        var text=message.ToLowerInvariant();var s=await site.GetAsync(ct);
        var products=await db.Products.Include(x=>x.Category).Where(x=>!x.Archived&&!x.Draft&&x.Available&&x.Category.Visible&&!x.Category.Archived).OrderBy(x=>x.SortOrder).ToArrayAsync(ct);
        if(!NvidiaMenuAssistant.InScope(message,products.Select(p=>p.Name)))return new(NvidiaMenuAssistant.Refusal,[],["Подобрать кофе","Что перекусить?"]);
        products=NvidiaMenuAssistant.Eligible(message,products);
        var suggestions=new[]{"Подобрать кофе","Что перекусить?","Как забрать заказ?"};
        if(text.Contains("достав")||text.Contains("самовывоз")||text.Contains("забрать"))return new($"Можно выбрать самовывоз из {s.CafeName} или доставку. Самовывоз бесплатный, доставка — {s.DeliveryPrice} ₽. Выберите удобное время при оформлении.",[],suggestions);
        if(text.Contains("время работ")||text.Contains("открыт")||text.Contains("адрес"))return new(string.Join(" ",new[]{s.Address.Length>0?s.Address:"Адрес кафе ещё уточняется.",s.Hours.Length>0?s.Hours:"График пока не опубликован."}),[],suggestions);
        var terms=Regex.Split(text,@"\W+").Where(x=>x.Length>2).ToArray();
        if(Regex.IsMatch(text,@"^(как|где|можно|нужно|что будет)\b")){
            var faq=s.Faq.Select(f=>new{Item=f,Score=terms.Count(t=>f.Question.Contains(t,StringComparison.OrdinalIgnoreCase))}).OrderByDescending(x=>x.Score).FirstOrDefault();
            if(faq?.Score>=2)return new(faq.Item.Answer,[],suggestions);
        }
        var found=products.Where(p=>terms.Any(t=>(p.Name+" "+p.Category.Name+" "+p.Ingredients).Contains(t,StringComparison.OrdinalIgnoreCase))).Take(4).ToArray();
        if(text.Contains("перекус")||text.Contains("поесть"))found=products.Where(p=>p.Unit!="мл"&&p.CategoryId is not("coffee" or "tea" or "cold" or "shakes")).Take(4).ToArray();
        var budget=Regex.Match(text,@"до\s*(\d{1,5})");if(budget.Success)found=products.Where(p=>p.Price<=int.Parse(budget.Groups[1].Value)).Take(4).ToArray();
        if(text.Contains("состав")||text.Contains("аллерг")||text.Contains("молок")){
            if(found.Length==0)found=products.Take(2).ToArray();
            return new(string.Join("\n\n",found.Select(p=>p.Name+": "+p.Ingredients))+"\n\nПодробные аллергены и изменения рецепта смотрите в карточке. Гарантировать отсутствие следов на общем оборудовании нельзя.",found.Select(p=>p.Id).ToArray(),suggestions);
        }
        if(found.Length>0)return new("Подойдут эти позиции. Нажмите «Настроить», выберите варианты и подтвердите цену перед добавлением в корзину.",found.Select(p=>p.Id).ToArray(),suggestions);
        return new("Помогу выбрать из текущего меню, расскажу о составе и получении заказа. Предпочитаете напиток или что-нибудь перекусить? Цены и доступность беру из каталога.",products.Take(2).Select(p=>p.Id).ToArray(),suggestions);
    }
}
