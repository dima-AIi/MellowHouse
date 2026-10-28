using System.Text.RegularExpressions;
using MellowHouse.Web.Data;

namespace MellowHouse.Web.Features;

/// <summary>Public menu context only. This never decides payment, checkout or private order ownership.</summary>
public static class AssistantConversation
{
    private static bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    public static bool IsDrink(Product p)=>p.CategoryId is "coffee" or "tea" or "cold" or "shakes";
    public static bool IsMenuQuestion(string text)=>Has(text,@"^\s*(?:(?:а|ну|тогда)\s+)*(?:что\s+у\s+вас\s+(?:есть|можно\s+выбрать)|что\s+посоветуешь|что\s+можно\s+выбрать|не\s+знаю\s+что\s+выбрать|помоги\s+выбрать)[.!?\s]*$");
    public static string? MealCategory(string text)
    {
        if(Has(text,@"салат|боул"))return "salads";
        if(Has(text,@"сэндвич|бутерброд|перекус"))return "sandwiches";
        if(Has(text,@"завтрак|сырник|омлет|гранол|вафл|круассан с яйцом"))return "breakfast";
        if(Has(text,@"десерт|выпечк|сладеньк"))return "desserts";
        if(Has(text,@"пицц"))return "pizza";
        if(Has(text,@"блюд|\bед[ауыойе]*\b|поесть|покуш|куша|обед|ужин|сытн|паст|\bсуп"))
            return Has(text,@"горяч|т[её]пл|сытн|паст|\bсуп")?"hot":"meals";
        return null;
    }
    public static string[] Suggestions(IEnumerable<Product> products,string? category=null,bool added=false)
    {
        var selected=products.ToArray();
        if(selected.Any(IsDrink)&&selected.Any(p=>!IsDrink(p)))
            return ["Другие блюда","Другой напиток","Что в составе?"];
        if(selected.Length>0&&selected.All(IsDrink)||category is "drinks" or "cold" or "warm-drinks" or "coffee" or "tea" or "shakes" or "matcha" or "bubble-tea")
            return added?["Подобрать горячее блюдо","Выбрать десерт","Другой напиток"]
                :["Другой напиток","Напиток без молока","До 400 рублей"];
        if(selected.Length>0&&selected.All(p=>p.CategoryId=="desserts")||category=="desserts")
            return ["Другой десерт","Подобрать кофе","Что в составе?"];
        if(selected.Length>0&&selected.All(p=>!IsDrink(p))||category is "hot" or "meals" or "salads" or "sandwiches" or "breakfast" or "pizza")
            return added?["Подобрать напиток","Выбрать десерт","Что в составе?"]
                :[category=="hot"?"Ещё горячие блюда":"Другие блюда","Блюдо без мяса","Подобрать напиток"];
        return ["Выбрать блюдо","Подобрать напиток","Как получить заказ?"];
    }
}
