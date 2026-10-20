using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Data;
public static class Seeder
{
    public static async Task SeedAsync(ShopDbContext db, string root, CancellationToken ct = default)
    {
        if (await db.SiteSettings.AnyAsync(x => x.Key == "catalog.initialized.v2", ct)) return;
        if (await db.Products.AnyAsync(ct) || await db.Categories.AnyAsync(ct))
        {
            foreach (var p in await db.Products.ToArrayAsync(ct)) p.Recommended = p.Popular;
            await FinishAsync(db, ct); return;
        }
        using var stream = File.OpenRead(Path.Combine(root, "Data", "catalog.json"));
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        foreach (var c in json.RootElement.GetProperty("categories").EnumerateArray())
        {
            var id = c.GetProperty("id").GetString()!;
            var category = await db.Categories.FindAsync([id], ct);
            if (category is null) { category = new Category { Id = id }; db.Categories.Add(category); }
            category.Name = c.GetProperty("name").GetString()!;
            category.SortOrder = c.GetProperty("sortOrder").GetInt32();
        }
        await db.SaveChangesAsync(ct);
        var ids = new HashSet<string>(); var index = 0;
        foreach (var p in json.RootElement.GetProperty("products").EnumerateArray())
        {
            var id = p.GetProperty("id").GetString()!; ids.Add(id);
            var product = await db.Products.FindAsync([id], ct);
            if (product is null) { product = new Product { Id = id }; db.Products.Add(product); }
            product.CategoryId = p.GetProperty("categoryId").GetString()!;
            product.Name = p.GetProperty("name").GetString()!;
            product.Price = p.GetProperty("price").GetInt32();
            product.Portion = p.GetProperty("portion").GetString()!;
            product.Description = p.GetProperty("description").GetString()!;
            product.Ingredients = p.GetProperty("ingredients").GetString()!;
            product.Image = p.GetProperty("image").GetString()!;
            product.Popular = p.GetProperty("popular").GetBoolean();
            product.Recommended = product.Popular;
            product.Available = !p.TryGetProperty("available", out var a) || a.GetBoolean();
            product.GroupsJson = p.GetProperty("groups").GetRawText();
            product.AllergensJson = p.GetProperty("allergens").GetRawText(); product.SortOrder = index++;
        }
        await db.SaveChangesAsync(ct);
        await FinishAsync(db, ct);
    }
    private static async Task FinishAsync(ShopDbContext db, CancellationToken ct)
    {
        var photos = new Dictionary<string, string> { ["breakfast"]="egg-croissant", ["sandwiches"]="roastbeef-mustard", ["salads"]="salmon-bowl", ["hot"]="beef-medallions", ["coffee"]="cappuccino", ["cold"]="cherry-lemonade", ["shakes"]="strawberry-shake", ["desserts"]="chocolate-fondant" };
        foreach(var c in await db.Categories.ToArrayAsync(ct)) if(photos.TryGetValue(c.Id,out var photo)) { c.Image="/images/categories/"+photo+".webp"; c.OnHome=true; }
        db.SiteSettings.Add(new() { Key="catalog.initialized.v2", Json="{}" }); await db.SaveChangesAsync(ct);
    }
}
