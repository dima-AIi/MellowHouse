using System.Text.Json;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Data;

public sealed record NutritionFacts(decimal Calories,decimal Protein,decimal Fat,decimal Carbs,bool Estimated=true);

public static class NutritionData
{
    public const string Marker="catalog.nutrition.v8";
    public static NutritionFacts? Read(string? json)
    {
        if(string.IsNullOrWhiteSpace(json)||!json.TrimStart().StartsWith('{'))return null;
        try{return JsonSerializer.Deserialize<NutritionFacts>(json,JsonDefaults.Options);}catch(JsonException){return null;}
    }
    public static void Validate(NutritionFacts facts)
    {
        if(facts.Calories is <0 or >10000||facts.Protein is <0 or >1000||facts.Fat is <0 or >1000||facts.Carbs is <0 or >1000)
            throw new ShopException("КБЖУ на порцию: калории от 0 до 10 000, белки, жиры и углеводы от 0 до 1 000 г.");
    }
    public static async Task SeedAsync(ShopDbContext db,string root,CancellationToken ct=default)
    {
        if(await db.SiteSettings.AnyAsync(s=>s.Key==Marker,ct))return;
        var data=JsonSerializer.Deserialize<Dictionary<string,NutritionFacts>>(await File.ReadAllTextAsync(Path.Combine(root,"Data","nutrition-estimates.json"),ct),JsonDefaults.Options)!;
        var updated=0;
        foreach(var p in await db.Products.ToArrayAsync(ct))
        {
            if(!string.IsNullOrWhiteSpace(p.Nutrition)||!data.TryGetValue(p.Id,out var facts))continue;
            Validate(facts);p.Nutrition=JsonSerializer.Serialize(facts,JsonDefaults.Options);updated++;
        }
        db.SiteSettings.Add(new(){Key=Marker,Json=JsonSerializer.Serialize(new{updated,estimated=true,basis="Ориентировочные значения на базовую порцию; технологические карты не предоставлены."},JsonDefaults.Options)});
        Audit.Log(db,Guid.Empty,"catalog.nutrition",Marker,$"Ориентировочное КБЖУ: {updated}; существующие значения сохранены");
        await db.SaveChangesAsync(ct);
    }
}
