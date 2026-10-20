using System.Text.Json;
using MellowHouse.Web.Data;

namespace MellowHouse.Web.Features;

public sealed class ShopException(string message, int status = 400, string? field = null) : Exception(message)
{
    public int Status { get; } = status;
    public string? Field { get; } = field;
}
public sealed record ConfigurationResult(string Canonical, Dictionary<string, string[]> Selections, int UnitPrice, string[] Summary, string[] Allergens);
public static class Pricing
{
    public static Dictionary<string, string[]> Parse(string config) => JsonSerializer.Deserialize<Dictionary<string, string[]>>(config, JsonDefaults.Options) ?? [];
    public static ConfigurationResult Calculate(ProductView product, Dictionary<string, string[]>? input)
    {
        if (!product.Available) throw new ShopException("Эта позиция сейчас недоступна.", 409);
        input ??= [];
        if (input.Keys.Any(key => product.Groups.All(g => g.Id != key))) throw new ShopException("У товара нет выбранной настройки.");
        var sorted = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        var summary = new List<string>();
        var allergens = new HashSet<string>(product.Allergens);
        var total = product.Price;
        foreach (var group in product.Groups)
        {
            var selected = input.TryGetValue(group.Id, out var supplied) ? supplied : group.Defaults;
            if (selected is null || selected.Any(x => x is null)) throw new ShopException("Проверьте настройки товара.");
            selected = selected.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if ((group.Required && selected.Length == 0) || (!group.Multiple && selected.Length > 1) || selected.Length < (group.Min ?? 0) || selected.Length > (group.Max ?? int.MaxValue)) throw new ShopException($"Проверьте количество вариантов: {group.Label}.");
            sorted[group.Id] = selected;
            foreach (var id in selected)
            {
                var option = group.Options.FirstOrDefault(o => o.Id == id) ?? throw new ShopException($"Недоступный вариант: {group.Label}.");
                total = checked(total + option.Price);
                summary.Add(option.Label);
                foreach (var a in option.RemoveAllergens ?? []) allergens.Remove(a);
                foreach (var a in option.AddAllergens ?? []) allergens.Add(a);
            }
        }
        if (total < 0) throw new ShopException("Цена конфигурации недоступна.");
        return new(JsonSerializer.Serialize(sorted, JsonDefaults.Options), sorted.ToDictionary(x => x.Key, x => x.Value), total, summary.ToArray(), allergens.Order().ToArray());
    }
}
