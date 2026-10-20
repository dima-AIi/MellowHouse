using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
using StbImageSharp;

namespace MellowHouse.Web.Features;

public static class Audit
{
    public static void Log(ShopDbContext db, Guid actor, string action, string target, string detail="") => db.AuditEntries.Add(new(){ActorId=actor,Action=action,Target=target,Detail=detail});
}
public sealed record ProductInput(string Name, string CategoryId, int Price, string Portion, string Description, string Ingredients, string Image,
    bool Available, bool Popular, bool Recommended, bool Draft, int SortOrder, int? Cost, decimal? Serving, string Unit, string Nutrition, string[] Allergens, OptionGroup[] Groups);
public sealed record CategoryInput(string Name, int SortOrder, bool Visible, string Image, bool OnHome);
public sealed record ArchiveCategoryRequest(string? TransferTo, bool WithProducts);
public sealed class CatalogAdminService(ShopDbContext db, MediaService media)
{
    public async Task<object> CatalogAsync(CancellationToken ct) => new { categories=await db.Categories.OrderBy(x=>x.SortOrder).Select(x=>new{x.Id,x.Name,x.SortOrder,x.Visible,x.Archived,x.Image,x.OnHome}).ToArrayAsync(ct),
        products=(await db.Products.OrderBy(x=>x.SortOrder).ToArrayAsync(ct)).Select(p=>new {p.Id,p.Name,p.CategoryId,p.Price,p.Portion,p.Description,p.Ingredients,p.Image,p.Available,p.Popular,p.Recommended,p.Draft,p.Archived,p.SortOrder,p.Cost,p.Serving,p.Unit,p.Nutrition,groups=JsonSerializer.Deserialize<OptionGroup[]>(p.GroupsJson,JsonDefaults.Options),allergens=JsonSerializer.Deserialize<string[]>(p.AllergensJson,JsonDefaults.Options)}) };
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
        p.Name=name;p.CategoryId=r.CategoryId;p.Price=r.Price;p.Portion=r.Portion.Trim();p.Description=r.Description.Trim();p.Ingredients=r.Ingredients.Trim();p.Image=r.Image;p.Available=r.Available;p.Popular=r.Popular;p.Recommended=r.Recommended;p.Draft=r.Draft;p.SortOrder=r.SortOrder;p.Cost=r.Cost;p.Serving=r.Serving;p.Unit=r.Unit;p.Nutrition=r.Nutrition.Trim();
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
public sealed class MediaService(IWebHostEnvironment env)
{
    public static bool SafePath(string path, params string[] prefixes)=>!string.IsNullOrEmpty(path)&&path.Length<220&&!path.Contains("..")&&!path.Contains('\\')&&Regex.IsMatch(path,@"^/[a-zA-Z0-9_./-]+$")&&prefixes.Any(path.StartsWith);
    public bool Exists(string path)=>SafePath(path,"/images/","/uploads/")&&File.Exists(Path.Combine(env.WebRootPath,path.TrimStart('/')));
    public bool AssetExists(string path)=>SafePath(path,"/images/","/uploads/","/videos/")&&File.Exists(Path.Combine(env.WebRootPath,path.TrimStart('/')));
    public async Task ValidateProductAsync(string path,CancellationToken ct)
    {
        if(!Exists(path))throw new ShopException("Добавьте фотографию товара с прозрачным фоном.");
        if(path is "/images/products/vanilla-raf.webp" or "/images/products/bbq-chicken.webp")return;
        if(Path.GetExtension(path)!=".png")throw new ShopException("Для товара загрузите PNG с настоящим прозрачным фоном.");
        var data=await File.ReadAllBytesAsync(Path.Combine(env.WebRootPath,path.TrimStart('/')),ct);Decode(data,true);
    }
    public async Task<object> UploadAsync(IFormFile file,bool product,CancellationToken ct)
    {
        if(file.Length is <32 or >8388608)throw new ShopException("Фотография должна быть меньше 8 МБ.");
        using var stream=new MemoryStream();await file.CopyToAsync(stream,ct);var data=stream.ToArray();
        var png=data.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10});var jpeg=data.Length>3&&data[0]==255&&data[1]==216&&data[2]==255;
        if(!png&&!jpeg)throw new ShopException("Загрузите PNG или JPG. Для товара нужен прозрачный PNG.");
        if(product&&!png)throw new ShopException("Для товара нужен PNG с прозрачным фоном.");
        var dimensions=Decode(data,product);var name=Guid.NewGuid().ToString("N")+(png?".png":".jpg");var dir=Path.Combine(env.WebRootPath,"uploads");Directory.CreateDirectory(dir);await File.WriteAllBytesAsync(Path.Combine(dir,name),data,ct);
        return new {path="/uploads/"+name,width=dimensions.Item1,height=dimensions.Item2,transparent=product};
    }
    private static (int,int) Decode(byte[] data,bool requireAlpha)
    {
        try{
            using var infoStream=new MemoryStream(data);var info=ImageInfo.FromStream(infoStream)??throw new ShopException("Не удалось прочитать фотографию.");
            if(info.Width is <32 or >4096 || info.Height is <32 or >4096 || (long)info.Width*info.Height>12000000)throw new ShopException("Размер фотографии: от 32 px, не более 4096 px и 12 млн пикселей.");
            var image=ImageResult.FromMemory(data,ColorComponents.RedGreenBlueAlpha);var transparent=0;var visible=0;
            for(var i=3;i<image.Data.Length;i+=4){if(image.Data[i]==0)transparent++;if(image.Data[i]>0)visible++;}
            if(requireAlpha&&(transparent<image.Width*image.Height/100||visible<image.Width*image.Height/100))throw new ShopException("Нужны видимый товар и настоящий прозрачный фон. Белый фон не подходит.");return(image.Width,image.Height);
        }catch(ShopException){throw;}catch{throw new ShopException("Файл повреждён или формат не поддерживается.");}
    }
}
