using System.Text.Json;
using System.Text.RegularExpressions;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Features;

public sealed record CafeAssistantReply(string Text,string[] ProductIds,string[] Suggestions,CartView? Cart=null,string[]? AddedProductIds=null,bool RequiresLogin=false);
public sealed class CafeAssistantService(ShopDbContext db,IMenuAssistant assistant,CartService carts,MutationGate gate,AssistantHistory history)
{
    private static readonly string[] MealCategories=["hot","pizza","sandwiches","salads","breakfast"];
    private static readonly string[] DrinkCategories=["coffee","tea","cold","shakes"];
    private static readonly string[] IcedCoffees=["iced-latte","cold-brew","espresso-tonic"];
    private static readonly string[] StartChoices=["Горячее и сытное","Салаты и боулы","Сэндвичи","Завтраки","Десерты","Напиток"];
    private static bool Has(string value,string pattern)=>Regex.IsMatch(value,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    public static bool WantsAdd(string message)=>!Has(message,@"не\s+(добав|кла|клад|полож|нужно)|^\s*(как|можно ли|умеешь|ты умеешь)\b")&&Has(message,@"\b(добавь(?:те)?|положи(?:те)?|закинь|возьми)\b|можешь\s+(?:пожалуйста\s+)?(?:добавить|положить)");
    private static string? Category(string text)=>Has(text,@"матч|matcha")?"matcha":Has(text,@"бабл[ -]?гам|bubble.?gum|коктейл|мохито|пина[ -]?колад")?"shakes":Has(text,@"бабл[ -]?ти|баббл|таро|bubble.?tea")?"bubble-tea":Has(text,@"горяч[а-я]*\s+напит")?"warm-drinks":Has(text,@"холодн.*напит|лимонад|айс\b")?"cold":Has(text,@"салат|боул|л[её]гко")?"salads":Has(text,@"сэндвич|бутерброд|перекус")?"sandwiches":Has(text,@"завтрак|сырник|омлет|гранол|вафл|круассан с яйцом")?"breakfast":Has(text,@"десерт|сладкое|сладеньк|выпечк")?"desserts":Has(text,@"пицц")?"pizza":Has(text,@"горячее|сытн|горяч[а-я]*\s+блюд|паст|суп|медальон|лосось с овощами")?"hot":Has(text,@"коф|эспрессо|капучино|латте|раф|фл[эе]т|колд[ -]?брю|тоник")?"coffee":Has(text,@"\bчай\b|какао")?"tea":Has(text,@"напит|попить|пить")?"drinks":null;
    private static Product[] InCategory(Product[] products,string category)=>category switch{
        "matcha"=>products.Where(p=>p.Id.Contains("matcha",StringComparison.Ordinal)).ToArray(),
        "bubble-tea"=>products.Where(p=>p.Id.EndsWith("bubble-tea",StringComparison.Ordinal)).ToArray(),
        "hot"=>products.Where(p=>p.CategoryId is "hot" or "pizza" or "sandwiches").ToArray(),
        "drinks"=>products.Where(p=>DrinkCategories.Contains(p.CategoryId)).ToArray(),
        "meals"=>products.Where(p=>MealCategories.Contains(p.CategoryId)).ToArray(),
        "warm-drinks"=>products.Where(p=>(p.CategoryId is "coffee" or "tea")&&!IcedCoffees.Contains(p.Id)).ToArray(),
        "cold"=>products.Where(p=>p.CategoryId is "cold" or "shakes"||IcedCoffees.Contains(p.Id)).ToArray(),
        _=>products.Where(p=>p.CategoryId==category).ToArray()};
    private static Product[] Rank(IEnumerable<Product> products)=>products.OrderByDescending(p=>(p.Popular||p.Recommended?100:0)+(p.CategoryId switch{"hot"=>40,"pizza"=>30,"sandwiches"=>25,"salads"=>15,"breakfast"=>10,_=>0})).ThenBy(p=>p.Price).ThenBy(p=>p.Id).ToArray();
    private static bool IsDrinkCategory(string? category)=>category is "drinks" or "cold" or "warm-drinks" or "coffee" or "tea" or "shakes" or "matcha" or "bubble-tea";
    private static string? PreviousCategory(string[] recent)
    {
        string? previous=null;
        foreach(var question in recent){var next=Category(question)??AssistantConversation.MealCategory(question);if(next=="drinks"&&IsDrinkCategory(previous)&&Has(question,@"друг|без\s|до\s*\d"))continue;previous=next??previous;}
        return previous;
    }
    private static Product[] ColdSuggestions(Product[] candidates)
    {
        // Keep a varied first selection as the menu grows: iced coffee, cold tea/lemonade, shake.
        var ranked=Rank(candidates);
        return ranked.Where(p=>IcedCoffees.Contains(p.Id)).Take(1)
            .Concat(ranked.Where(p=>p.CategoryId=="cold").Take(1))
            .Concat(ranked.Where(p=>p.CategoryId=="shakes").Take(1))
            .Concat(ranked).DistinctBy(p=>p.Id).Take(3).ToArray();
    }
    private static Product[] Named(string message,Product[] products)
    {
        var alias=Has(message,@"бабл[ -]?гам|bubble.?gum")?"bubblegum-shake":Has(message,@"таро")?"taro-bubble-tea":Has(message,@"(?:мангов[а-я]*|манго).*матч|матч.*манго")?"mango-matcha":Has(message,@"клубнич[а-я]*.*матч|матч.*клубнич")?"strawberry-matcha":Has(message,@"бабл[ -]?ти.*манго|манго.*бабл[ -]?ти")?"mango-bubble-tea":null;
        if(alias is not null)return products.Where(p=>p.Id==alias).ToArray();
        var exact=products.Where(p=>Regex.IsMatch(message,@"(?<!\p{L})"+Regex.Escape(p.Name)+@"(?!\p{L})",RegexOptions.IgnoreCase)).ToArray();
        if(exact.Length>0)return exact.Where(p=>!exact.Any(other=>other!=p&&other.Name.Contains(p.Name,StringComparison.OrdinalIgnoreCase))).ToArray();
        var words=Regex.Matches(message.ToLowerInvariant(),@"[\p{L}]+\d*").Select(x=>x.Value).ToHashSet();
        return products.Where(p=>p.Name.ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries).Any(word=>word.Length>=4&&!Regex.IsMatch(word,@"^(горяч|холод|сэндв|салат|пицц|завтр|напит|слад|кофе)")&&words.Any(w=>w.Length>=4&&w.StartsWith(word[..Math.Min(word.Length,5)],StringComparison.Ordinal)))||p.Name.Contains("раф",StringComparison.OrdinalIgnoreCase)&&words.Contains("раф")).ToArray();
    }
    public async Task<CafeAssistantReply> ReplyAsync(AssistantRequest r,string owner,CancellationToken ct)
    {
        var message=Inputs.Required(r.Message,"вопрос",500);var text=message.ToLowerInvariant();
        var all=await db.Products.Include(p=>p.Category).Where(p=>p.Available&&!p.Archived&&!p.Draft&&p.Category.Visible&&!p.Category.Archived).ToArrayAsync(ct);
        if(AssistantHistory.IsQuestion(message)&&!Has(message,@"игнорир|промпт|забудь.*инструк"))return await history.ReplyAsync(message,owner,all,ct);
        var recent=NvidiaMenuAssistant.Recent(r.RecentQuestions);var lastIds=(r.LastProductIds??[]).Where(x=>x is {Length:<=80}).Distinct().Take(6).ToArray();
        var previousCategory=PreviousCategory(recent);var currentCategory=Category(text);
        var mealCategory=AssistantConversation.MealCategory(text);
        var explicitDrink=currentCategory is "drinks" or "cold" or "warm-drinks" or "coffee" or "tea" or "shakes" or "matcha" or "bubble-tea";
        if(mealCategory=="meals")mealCategory=recent.Select(AssistantConversation.MealCategory).LastOrDefault(x=>x is not null and not "meals")??mealCategory;
        if(mealCategory is not null&&!explicitDrink)currentCategory=mealCategory;
        if(currentCategory=="drinks"&&IsDrinkCategory(previousCategory)&&Has(text,@"друг|без\s|до\s*\d"))currentCategory=previousCategory;
        if(previousCategory=="drinks"&&Has(text,@"^(?:хочу\s+)?горяч(?:ее|ий|его)[.!\s]*$"))currentCategory="warm-drinks";
        if(previousCategory=="drinks"&&Has(text,@"^(?:хочу\s+)?холодн(?:ое|ый|ого)[.!\s]*$"))currentCategory="cold";
        var category=currentCategory??(NvidiaMenuAssistant.Followup(text)||WantsAdd(text)?previousCategory:null);
        if(currentCategory is null&&mealCategory is not null)category=mealCategory;
        if(NvidiaMenuAssistant.OffTopic(message)||!NvidiaMenuAssistant.InScope(message,all.Select(p=>p.Name))&&!(NvidiaMenuAssistant.Followup(message)&&(recent.Length>0||lastIds.Length>0)))return new(NvidiaMenuAssistant.Refusal,[],StartChoices);
        var constraints=string.Join(" ",recent.TakeLast(2))+" "+message;
        // Category intent is resolved here; retain explicit diet/budget rules without accidentally filtering all food as coffee.
        var restrictionWords=string.Join(" ",Regex.Matches(constraints,@"до\s*\d{1,5}|без\s+молок[а-я]*|безлактоз[а-я]*|без\s+мяс[а-я]*|вегетар[а-я]*",RegexOptions.IgnoreCase).Select(m=>m.Value));
        var eligible=NvidiaMenuAssistant.Eligible(restrictionWords,all);
        if(Has(constraints,@"с куриц|куриное|куриный")&&!Has(constraints,@"без куриц"))eligible=eligible.Where(p=>AssistantConversation.IsDrink(p)||Has(p.Ingredients,@"куриц|курин")).ToArray();
        if(!WantsAdd(text)&&Has(text,@"^\s*(?:а\s+)?(?:почему|чем\b.*?отлич|в ч[её]м\s+(?:разниц|отлич)|какая\s+разница|что\s+такое|расскажи\s+(?:о|про)|как\s+(?:готов|дела))"))
        {
            var explanation=await assistant.ReplyAsync(message,ct,recent);return new(explanation.Text,explanation.ProductIds,explanation.Suggestions);
        }
        if(mealCategory is not null&&explicitDrink&&!WantsAdd(text))
        {
            var meals=Rank(InCategory(eligible,mealCategory)).Take(2).ToArray();
            if(currentCategory=="drinks")return new("Начнём с еды: вот подходящие блюда. Напиток тоже подберём — хочется горячий или холодный? Ничего в корзину пока не добавлял.",meals.Select(p=>p.Id).ToArray(),["Холодный напиток","Горячий напиток","Ещё горячие блюда"]);
            var drinks=currentCategory=="cold"?ColdSuggestions(InCategory(eligible,currentCategory)):Rank(InCategory(eligible,currentCategory!)).Take(2).ToArray();
            var budget=Regex.Match(restrictionWords,@"до\s*(\d{1,5})");
            if(budget.Success){var limit=int.Parse(budget.Groups[1].Value);var pair=(from food in meals from drink in drinks where food.Price+drink.Price<=limit select new[]{food,drink}).FirstOrDefault();if(pair is null)return new("Для блюда вместе с напитком этот бюджет маловат. Подобрать только блюдо или увеличить общую сумму?",[],["Выбрать только блюдо","Подобрать только напиток"]);return new("Вот блюдо и напиток, которые вместе укладываются в бюджет по базовым ценам. Настройки можно проверить отдельно; корзину пока не менял.",pair.Select(p=>p.Id).ToArray(),["Что в составе?","Другие блюда","Другой напиток"]);}
            return new("Вот варианты еды и напитков к ней. Выберите по одному — каждую позицию можно настроить и добавить отдельно. Корзину пока не менял.",meals.Concat(drinks.Take(2)).Select(p=>p.Id).ToArray(),["Другие блюда","Другой напиток","Что в составе?"]);
        }
        if(Has(text,@"не знаю|не определил|помоги выбрать|что (?:бы )?(?:мне )?поесть|что хочу")&&!WantsAdd(text)&&category is null)
            return new("Давайте выберем по настроению. Хочется горячего и сытного, лёгкого блюда или чего-нибудь сладкого? Можно начать с категории ниже. Если хотите сюрприз, напишите: «Сам выбери мне еду и добавь в корзину».",[],StartChoices);
        if(Has(text,@"холодн")&&currentCategory is null&&!WantsAdd(text))return new("Хочется холодный напиток или лёгкое блюдо?",[],["Холодный напиток","Салаты и боулы"]);
        if(currentCategory=="drinks"&&!WantsAdd(text))return new("Какой напиток хочется — горячий или холодный?",[],["Горячий напиток","Холодный напиток"]);
        var named=Named(message,eligible);var candidates=category is null?eligible:InCategory(eligible,category);
        if(WantsAdd(message))
        {
            Product[] selected=[];var offered=lastIds.Select(id=>eligible.FirstOrDefault(p=>p.Id==id)).Where(p=>p is not null).Cast<Product>().ToArray();
            var index=Has(text,@"перв(?:ый|ое|ую|ого)")?0:Has(text,@"втор(?:ой|ое|ую|ого)")?1:Has(text,@"трет(?:ий|ье|ью|ьего)")?2:-1;
            if(index>=0){var chosen=index<lastIds.Length?eligible.FirstOrDefault(p=>p.Id==lastIds[index]):null;if(chosen is not null)selected=[chosen];}
            else if(named.Length==1)selected=named;
            else if(Has(text,@"\b(все|всё|оба|обе|их)\b")&&offered.Length>0)selected=offered.Take(3).ToArray();
            else if(Has(text,@"сам\s+выбер|на твой вкус|на тво[её] усмотрение|сюрприз|что[ -]нибудь|любое"))
            {
                var pool=category is null?eligible.Where(p=>MealCategories.Contains(p.CategoryId)):candidates;
                selected=Rank(pool).Take(1).ToArray();
            }
            else if(offered.Length==1)selected=offered;
            if(selected.Length==0)return new("Уточните позицию: напишите её название или «добавь первый/второй» из последнего списка. Могу сам выбрать из подходящих — если попросите.",Rank(candidates).Take(3).Select(p=>p.Id).ToArray(),AssistantConversation.Suggestions(candidates,category));
            if(Has(constraints,@"аллерги|сильн.*неперенос"))return new("При аллергии сначала уточним состав и доступные исключения. На общем оборудовании возможны следы аллергенов — я не буду добавлять случайное блюдо. Посмотрите эти карточки и обсудите приготовление с кафе.",selected.Select(p=>p.Id).ToArray(),["Что в составе?","Контакты кафе"]);
            if(!Guid.TryParse(r.RequestId,out var requestId))throw new ShopException("Обновите сообщение и повторите добавление.");
            var quantityMatch=Regex.Match(text,@"(?:добавь|положи|возьми)\s+(\d{1,5})\b");var quantity=quantityMatch.Success?int.Parse(quantityMatch.Groups[1].Value):1;
            if(quantity is <1 or >5)throw new ShopException("За один раз могу добавить от 1 до 5 порций. Уточните количество.");
            return await AddAsync(selected,quantity,message,restrictionWords,owner,requestId,ct);
        }
        if(Has(text,@"состав|ингредиент|аллерген"))
        {
            var details=named.Length>0?named:lastIds.Select(id=>eligible.FirstOrDefault(p=>p.Id==id)).OfType<Product>().ToArray();
            if(details.Length>0)return new(string.Join("\n\n",details.Take(3).Select(p=>p.Name+": "+p.Ingredients+". Аллергены: "+string.Join(", ",p.View().Allergens)))+"\nПри аллергии уточните приготовление в кафе: возможен контакт на общем оборудовании.",details.Take(3).Select(p=>p.Id).ToArray(),["Покажи другие варианты","Подбери напиток"]);
        }
        if(category is not null||named.Length>0||Has(text,@"популярн|другие варианты|ещ[её] варианты"))
        {
            if(Has(text,@"другие варианты|ещ[её] варианты|друг(?:ой|ие|ую).*напит|другие блюд|ещ[её].*(?:напит|блюд|горяч)"))candidates=candidates.Where(p=>!lastIds.Contains(p.Id)).ToArray();
            var found=named.Length==0&&category=="cold"?ColdSuggestions(candidates):Rank(named.Length>0?named:candidates).Take(3).ToArray();
            var selectionText=found.Length>0?(AssistantConversation.MealCategory(message) is not null?"Давайте выберем еду: вот подходящие блюда. Можно посмотреть состав, настроить порцию или написать «добавь второй».":"Вот подходящие варианты из нашего меню. Можно открыть настройки или написать, например, «добавь второй». Хотите уточнить вкус или бюджет?"):"Под эти условия пока нет подходящих позиций. Изменим категорию или бюджет?";
            return new(selectionText,found.Select(p=>p.Id).ToArray(),AssistantConversation.Suggestions(found,category));
        }
        if(Has(text,@"умеешь|можешь")&&Has(text,@"добавля|корзин"))return new("Да. Назовите блюдо или попросите меня выбрать из популярных и добавить в корзину. Я покажу состав заказа и цену; проверить и изменить его можно в корзине. Оформление и оплата — за вами.",[],StartChoices);
        var answer=await assistant.ReplyAsync(message,ct,r.RecentQuestions);return new(answer.Text,answer.ProductIds,answer.Suggestions);
    }
    private static Dictionary<string,string[]> Configure(Product p,string text)
    {
        var view=p.View();var selections=view.Groups.ToDictionary(g=>g.Id,g=>g.Defaults.ToArray());
        foreach(var group in view.Groups)
        {
            var exclusion=Has(group.Id+" "+group.Label,@"excl|исключ|убрат|без ингреди");
            var chosen=new List<string>();
            foreach(var option in group.Options)
            {
                var words=Regex.Matches(option.Label.ToLowerInvariant(),@"[\p{L}]{3,}|\d+").Select(m=>m.Value).Where(w=>w is not("мл" or "грамм" or "без" or "или")&&!Has(w,@"^(добав|дополнительн|убрат|исключит)")).ToArray();
                var match=words.Any(w=>w.All(char.IsDigit)?Regex.IsMatch(text,@"\b"+Regex.Escape(w)+@"\s*(?:мл|г\b|$)"):text.Contains(w[..Math.Min(w.Length,4)],StringComparison.OrdinalIgnoreCase));
                if(!match)continue;
                var negative=words.Any(w=>!w.All(char.IsDigit)&&Regex.IsMatch(text,@"(?:без|убери|исключи)\s+(?:\w+\s+){0,2}"+Regex.Escape(w[..Math.Min(w.Length,4)]),RegexOptions.IgnoreCase));
                if(exclusion?!negative:negative&&!option.Label.StartsWith("Без",StringComparison.OrdinalIgnoreCase))continue;
                chosen.Add(option.Id);
            }
            if(chosen.Count>0)selections[group.Id]=group.Multiple?chosen.Distinct().Take(group.Max??chosen.Count).ToArray():[chosen[^1]];
        }
        return selections;
    }
    private async Task<CafeAssistantReply> AddAsync(Product[] products,int quantity,string message,string restrictions,string owner,Guid requestId,CancellationToken ct)
    {
        var key="assistant.action.v5."+Inputs.Hash(owner)[..24]+"."+requestId.ToString("N");var hash=Inputs.Hash(message+string.Join('|',products.Select(p=>p.Id))+quantity);
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            var existing=await db.SiteSettings.FindAsync([key],ct);
            if(existing is not null){var receipt=JsonSerializer.Deserialize<AssistantCartReceipt>(existing.Json,JsonDefaults.Options)!;if(receipt.Hash!=hash)throw new ShopException("Это сообщение уже было обработано. Отправьте новое.",409);return new(receipt.Text,receipt.ProductIds,AssistantConversation.Suggestions(products,added:true),await carts.ViewAsync(owner,ct),receipt.ProductIds);}
            await using var tx=await db.Database.BeginTransactionAsync(ct);var summaries=new List<string>();var total=0;
            foreach(var p in products){var selections=Configure(p,message);var price=Pricing.Calculate(p.View(),selections);total=checked(total+price.UnitPrice*quantity);summaries.Add(p.Name+" · "+quantity+" шт. · "+price.UnitPrice*quantity+" ₽"+(price.Summary.Length>0?" ("+string.Join(", ",price.Summary)+")":""));await carts.AddAsync(owner,new CartChange(p.Id,selections,quantity),ct);}
            var budget=Regex.Matches(restrictions,@"до\s*(\d{1,5})").LastOrDefault();if(budget is not null&&total>int.Parse(budget.Groups[1].Value))throw new ShopException("С выбранными настройками сумма превышает бюджет. Уточните размер или добавки — я пока ничего не добавил.");
            var text="Добавлено в корзину:\n"+string.Join("\n",summaries)+"\n\nМожно проверить и изменить заказ в корзине. Оформление и оплата ещё не выполнены.";
            db.SiteSettings.Add(new(){Key=key,Json=JsonSerializer.Serialize(new AssistantCartReceipt(hash,text,products.Select(p=>p.Id).ToArray()),JsonDefaults.Options)});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            return new(text,products.Select(p=>p.Id).ToArray(),AssistantConversation.Suggestions(products,added:true),await carts.ViewAsync(owner,ct),products.Select(p=>p.Id).ToArray());
        }
        finally{gate.Semaphore.Release();}
    }
    private sealed record AssistantCartReceipt(string Hash,string Text,string[] ProductIds);
}
