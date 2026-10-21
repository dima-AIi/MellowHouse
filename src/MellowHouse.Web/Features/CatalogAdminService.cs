using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Features;

public static class Audit
{
    public static void Log(ShopDbContext db, Guid actor, string action, string target, string detail="") => db.AuditEntries.Add(new(){ActorId=actor,Action=action,Target=target,Detail=detail});
}
public sealed record ProductInput(string Name, string CategoryId, int Price, string Portion, string Description, string Ingredients, string Image,
    bool Available, bool Popular, bool Recommended, bool Draft, int SortOrder, int? Cost, decimal? Serving, string Unit, string Nutrition, string[] Allergens, OptionGroup[] Groups, NutritionFacts? NutritionFacts=null);
public sealed record CategoryInput(string Name, int SortOrder, bool Visible, string Image, bool OnHome);
public sealed record ArchiveCategoryRequest(string? TransferTo, bool WithProducts);
public sealed class CatalogAdminService(ShopDbContext db, MediaService media)
{
    public async Task<object> CatalogAsync(CancellationToken ct) => new { categories=await db.Categories.OrderBy(x=>x.SortOrder).Select(x=>new{x.Id,x.Name,x.SortOrder,x.Visible,x.Archived,x.Image,x.OnHome}).ToArrayAsync(ct),
        products=(await db.Products.OrderBy(x=>x.SortOrder).ToArrayAsync(ct)).Select(p=>new {p.Id,p.Name,p.CategoryId,p.Price,p.Portion,p.Description,p.Ingredients,p.Image,p.Available,p.Popular,p.Recommended,p.Draft,p.Archived,p.SortOrder,p.Cost,p.Serving,p.Unit,p.Nutrition,nutritionFacts=NutritionData.Read(p.Nutrition),groups=JsonSerializer.Deserialize<OptionGroup[]>(p.GroupsJson,JsonDefaults.Options),allergens=JsonSerializer.Deserialize<string[]>(p.AllergensJson,JsonDefaults.Options)}) };
    public async Task<Product> SaveProductAsync(string? id, ProductInput r, Guid actor, CancellationToken ct)
    {
        if(r.Allergens is null||r.Groups is null||new[]{r.CategoryId,r.Name,r.Portion,r.Description,r.Ingredients,r.Image,r.Unit,r.Nutrition}.Any(x=>x is null))throw new ShopException("Заполните сведения о товаре и его настройках.");
        var p=id is null?new Product{Id="p-"+Guid.NewGuid().ToString("N")}:await db.Products.FindAsync([id],ct)??throw new ShopException("Товар не найден.",404);
        var category=await db.Categories.FindAsync([r.CategoryId],ct)??throw new ShopException("Выберите категорию.");
        if(category.Archived)throw new ShopException("Восстановите категорию перед добавлением товара.");
        var name=Inputs.Required(r.Name,"название товара",120);
        if(r.Price is < 0 or > 999999 || r.Cost is < 0 or > 999999 || r.Serving is <=0 or >10000 || r.SortOrder is <0 or >100000)throw new ShopException("Проверьте цену, себестоимость, порцию и порядок.");
        if(r.Unit.Length>20||r.Portion.Length>80||r.Description.Length>1000||r.Ingredients.Length>1500||r.Nutrition.Length>400||r.Allergens.Length>30||r.Allergens.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>60))throw new ShopException("Сократите сведения о товаре.");
        if(!r.Draft){Inputs.Required(r.Description,"описание",1000);Inputs.Required(r.Ingredients,"состав",1500);Inputs.Required(r.Portion,"порцию",80);await media.ValidateProductAsync(r.Image,ct);}
        else if(!string.IsNullOrWhiteSpace(r.Image))await media.ValidateProductAsync(r.Image,ct);
        ValidateGroups(r.Groups);
        if(r.Price+r.Groups.Sum(g=>g.Multiple?g.Options.Where(o=>o.Price<0).Sum(o=>o.Price):Math.Min(0,g.Options.Min(o=>o.Price)))<0)throw new ShopException("Настройки не должны приводить к отрицательной цене.");
        if(r.NutritionFacts is not null)NutritionData.Validate(r.NutritionFacts);
        p.Name=name;p.CategoryId=r.CategoryId;p.Price=r.Price;p.Portion=r.Portion.Trim();p.Description=r.Description.Trim();p.Ingredients=r.Ingredients.Trim();p.Image=r.Image;p.Available=r.Available;p.Popular=r.Popular;p.Recommended=r.Recommended;p.Draft=r.Draft;p.SortOrder=r.SortOrder;p.Cost=r.Cost;p.Serving=r.Serving;p.Unit=r.Unit;p.Nutrition=r.NutritionFacts is null?r.Nutrition.Trim():JsonSerializer.Serialize(r.NutritionFacts,JsonDefaults.Options);
        p.GroupsJson=JsonSerializer.Serialize(r.Groups,JsonDefaults.Options);p.AllergensJson=JsonSerializer.Serialize(r.Allergens.Distinct().ToArray(),JsonDefaults.Options);
        if(id is null)db.Products.Add(p);Audit.Log(db,actor,id is null?"product.created":"product.updated",p.Id,p.Name);await db.SaveChangesAsync(ct);return p;
    }
    public static void ValidateGroups(OptionGroup[] groups)
    {
        if(groups.Length>15||groups.Select(x=>x.Id).Distinct().Count()!=groups.Length)throw new ShopException("До 15 групп с уникальными идентификаторами.");
        foreach(var g in groups)
        {
            if(g is null||g.Options is null||g.Defaults is null||g.Options.Any(x=>x is null))throw new ShopException("Проверьте группу настроек и варианты.");
            if(!ValidId(g.Id)||string.IsNullOrWhiteSpace(g.Label)||g.Label.Length>100||g.Options.Length is <1 or >30||g.Options.Select(x=>x.Id).Distinct().Count()!=g.Options.Length)throw new ShopException("У группы должны быть название и от 1 до 30 уникальных вариантов.");
            var min=Math.Max(g.Min??0,g.Required?1:0);var max=g.Multiple?(g.Max??g.Options.Length):1;
            if(min<0||min>max||max>g.Options.Length||max<1||g.Defaults.Distinct().Count()!=g.Defaults.Length||g.Defaults.Length<min||g.Defaults.Length>max||g.Defaults.Any(x=>g.Options.All(o=>o.Id!=x)))throw new ShopException($"Проверьте ограничения и значения по умолчанию: {g.Label}.");
            if(!g.Multiple&&g.Max>1)throw new ShopException("Одиночная группа допускает только один вариант.");
            foreach(var o in g.Options)if(!ValidId(o.Id)||string.IsNullOrWhiteSpace(o.Label)||o.Label.Length>100||o.Price is < -99999 or >99999||o.Cost is <0 or >99999||(o.AddAllergens??[]).Concat(o.RemoveAllergens??[]).Any(a=>a.Length>60))throw new ShopException($"Проверьте варианты: {g.Label}.");
        }
    }
    private static bool ValidId(string id)=>!string.IsNullOrEmpty(id)&&Regex.IsMatch(id,@"^[a-z0-9][a-z0-9-]{0,39}$");
    public async Task<Category> SaveCategoryAsync(string? id,CategoryInput r,Guid actor,CancellationToken ct)
    {
        var name=Inputs.Required(r.Name,"название категории",100);if(r.SortOrder is <0 or >100000)throw new ShopException("Проверьте порядок категории.");
        if(r.Image!=""&&!media.Exists(r.Image))throw new ShopException("Выберите загруженное локальное фото.");
        if(r.OnHome&&string.IsNullOrWhiteSpace(r.Image))throw new ShopException("Для категории на главной нужно фото.");
        var c=id is null?new Category{Id="c-"+Guid.NewGuid().ToString("N")}:await db.Categories.FindAsync([id],ct)??throw new ShopException("Категория не найдена.",404);
        c.Name=name;c.SortOrder=r.SortOrder;c.Visible=r.Visible;c.Image=r.Image;c.OnHome=r.OnHome;if(id is null)db.Categories.Add(c);
        Audit.Log(db,actor,id is null?"category.created":"category.updated",c.Id,name);await db.SaveChangesAsync(ct);return c;
    }
    public async Task ArchiveProductAsync(string id,bool archived,Guid actor,CancellationToken ct)
    {
        var p=await db.Products.Include(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ShopException("Товар не найден.",404);
        if(!archived&&p.Category.Archived)throw new ShopException("Сначала восстановите категорию.");p.Archived=archived;Audit.Log(db,actor,archived?"product.archived":"product.restored",id,p.Name);await db.SaveChangesAsync(ct);
    }
    public async Task ArchiveCategoryAsync(string id, bool archived, ArchiveCategoryRequest r,Guid actor,CancellationToken ct)
    {
        var c=await db.Categories.FindAsync([id],ct)??throw new ShopException("Категория не найдена.",404);
        var products=await db.Products.Where(x=>x.CategoryId==id&&!x.Archived).ToArrayAsync(ct);
        if(archived&&products.Length>0)
        {
            if(!string.IsNullOrEmpty(r.TransferTo)){var target=await db.Categories.FindAsync([r.TransferTo],ct);if(target is null||target.Archived||target.Id==id)throw new ShopException("Выберите другую действующую категорию.");foreach(var p in products)p.CategoryId=target.Id;}
            else if(r.WithProducts){foreach(var p in products)p.Archived=true;}
            else throw new ShopException("В категории есть товары. Выберите перенос либо архивирование вместе с товарами.");
        }
        c.Archived=archived;Audit.Log(db,actor,archived?"category.archived":"category.restored",id,c.Name);await db.SaveChangesAsync(ct);
    }
}
