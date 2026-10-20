using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Data;

/// <summary>Adds the remaining agreed menu once; existing and archived products are never overwritten.</summary>
public static class MenuCompletion
{
    public const string Marker = "catalog.complete.v6";

    // The caller's expansion transaction covers products, marker and audit together.
    public static async Task ApplyAsync(ShopDbContext db, string root, CancellationToken ct = default)
    {
        if (await db.SiteSettings.AnyAsync(x => x.Key == Marker, ct)) return;
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "Data", "catalog-completion.json"), ct));
        var categories = await db.Categories.Select(x => x.Id).ToHashSetAsync(ct);
        var existing = await db.Products.Select(x => x.Id).ToHashSetAsync(ct);
        var added = 0;
        foreach (var item in file.RootElement.EnumerateArray())
        {
            var view = JsonSerializer.Deserialize<ProductView>(item.GetRawText(), JsonDefaults.Options)!;
            if (existing.Contains(view.Id)) continue;
            if (!categories.Contains(view.CategoryId)) throw new InvalidOperationException($"Unknown menu category: {view.CategoryId}");
            if (!MediaService.SafePath(view.Image, "/images/products/") || !File.Exists(Path.Combine(root, "wwwroot", view.Image.TrimStart('/'))))
                throw new InvalidOperationException($"Menu image is missing: {view.Id}");
            CatalogAdminService.ValidateGroups(view.Groups);
            var defaults = view.Groups.ToDictionary(g => g.Id, g => g.Defaults);
            if (Pricing.Calculate(view, defaults).UnitPrice != view.Price) throw new InvalidOperationException($"Default price mismatch: {view.Id}");
            var portion = Regex.Match(view.Portion, @"(\d+)\s*(г|мл)$");
            db.Products.Add(new Product
            {
                Id = view.Id, CategoryId = view.CategoryId, Name = view.Name, Price = view.Price,
                Portion = view.Portion, Description = view.Description, Ingredients = view.Ingredients,
                Image = view.Image, Available = true, Popular = view.Popular, Recommended = view.Popular,
                SortOrder = 300 + item.GetProperty("sortOrder").GetInt32(),
                GroupsJson = JsonSerializer.Serialize(view.Groups, JsonDefaults.Options),
                AllergensJson = JsonSerializer.Serialize(view.Allergens, JsonDefaults.Options),
                Serving = portion.Success ? decimal.Parse(portion.Groups[1].Value, CultureInfo.InvariantCulture) : null,
                Unit = portion.Success ? portion.Groups[2].Value : ""
            });
            existing.Add(view.Id); added++;
        }
        db.SiteSettings.Add(new SiteSetting { Key = Marker, Json = JsonSerializer.Serialize(new { added, plannedOriginal = 73, newDrinks = 5 }, JsonDefaults.Options) });
        Audit.Log(db, Guid.Empty, "catalog.completed", Marker, $"Added {added} products; existing products preserved.");
        await db.SaveChangesAsync(ct);
    }
}
