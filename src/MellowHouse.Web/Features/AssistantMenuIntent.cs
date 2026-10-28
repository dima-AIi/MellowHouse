using System.Text.RegularExpressions;

namespace MellowHouse.Web.Features;

/// <summary>Extracts every requested menu category without making cart or payment decisions.</summary>
public sealed record AssistantMenuIntent(string SelectionText,string[] Categories,string[] ExcludedCategories)
{
    private const RegexOptions Options=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant;
    // More specific phrases precede their overlapping generic terms (matcha latte is not coffee).
    private static readonly (string Category,string Pattern)[] Terms=[
        ("matcha",@"(?:(?:клубнич\p{L}*|мангов\p{L}*|манго)\s+)?(?:матч\p{L}*|matcha)(?:[ -]латте)?"),
        ("bubble-tea",@"(?:бабл[ -]?ти|баббл[ -]?ти|bubble[ -]?tea|таро)"),
        ("shakes",@"бабл[ -]?гам|bubble[ -]?gum|коктейл\p{L}*|мохито|пина[ -]?колад\p{L}*"),
        ("warm-drinks",@"горяч\p{L}*\s+напит\p{L}*"),
        ("cold",@"холодн\p{L}*\s+напит\p{L}*|лимонад\p{L}*|смузи|айс\b"),
        ("salads",@"салат\p{L}*|боул\p{L}*|цезарь"),
        ("sandwiches",@"с[эеа]ндвич\p{L}*|бутерброд\p{L}*|панини|перекус\p{L}*"),
        ("breakfast",@"завтрак\p{L}*|сырник\p{L}*|омлет\p{L}*|гранол\p{L}*|вафл\p{L}*|круассан\s+с\s+яйцом"),
        ("desserts",@"десерт\p{L}*|сладкое|сладеньк\p{L}*|выпечк\p{L}*|чизкейк\p{L}*|тирамису|эклер\p{L}*|брауни"),
        ("pizza",@"пицц\p{L}*"),
        ("hot",@"горячее|сытн\p{L}*|горяч\p{L}*\s+блюд\p{L}*|паст(?:а|у|ы|е|ой)?\b|суп(?:а|у|ом|е|ы|ов)?\b|медальон\p{L}*|лосось\s+с\s+овощами"),
        ("coffee",@"коф\p{L}*|эспрессо|американо|капучино|латте|\bраф\b|фл[эе]т[ -]?уайт|колд[ -]?брю|тоник"),
        ("tea",@"\bча[йюя]\b|какао"),
        ("drinks",@"напит\p{L}*|попить|\bпить\b")];

    public static AssistantMenuIntent Parse(string message)
    {
        var corrections=Regex.Matches(message,@"\b(?:передумал[аи]?|а\s+нет|(?:а|но)\s+лучше|нет\s*[,—-]?\s*лучше|вместо\s+этого)\b",Options);
        var text=corrections.Count>0?message[corrections[^1].Index..]:message;
        var excluded=new List<string>();
        // Preserve diet phrases such as "без молока"; only remove actual declined menu choices.
        text=Regex.Replace(text,@"\bне\s+(?:хочу|надо|нужно|буду|предлагай|добавляй)\s+.*?(?=[,;.!?]|\b(?:а|но|зато|лучше)\b|$)",m=>{
            var declined=Find(m.Value);if(declined.Length==0)return m.Value;
            excluded.AddRange(declined);return new string(' ',m.Length);
        },Options);
        foreach(var (_,pattern) in Terms)
            text=Regex.Replace(text,@"\b(?:не|без)\s+(?:"+pattern+@")|(?:"+pattern+@")\s+не\s+(?:хочу|надо|нужно|буду)\b",m=>{
                excluded.AddRange(Find(m.Value));return new string(' ',m.Length);
            },Options);
        var categories=Find(text);
        // A generic "напиток" alongside a named drink does not create a second drink request.
        if(categories.Any(c=>c is "coffee" or "tea" or "cold" or "warm-drinks" or "shakes" or "matcha" or "bubble-tea"))categories=categories.Where(c=>c!="drinks").ToArray();
        return new(text,categories,excluded.Except(categories).Distinct().ToArray());
    }

    private static string[] Find(string text)
    {
        var occupied=new List<(int Start,int End)>();var found=new List<(int Index,string Category)>();
        foreach(var (category,pattern) in Terms)
            foreach(Match match in Regex.Matches(text,@"(?<!\p{L})(?:"+pattern+@")",Options))
                if(!occupied.Any(span=>match.Index<span.End&&match.Index+match.Length>span.Start))
                {occupied.Add((match.Index,match.Index+match.Length));found.Add((match.Index,category));}
        return found.OrderBy(x=>x.Index).Select(x=>x.Category).Distinct().ToArray();
    }
}
