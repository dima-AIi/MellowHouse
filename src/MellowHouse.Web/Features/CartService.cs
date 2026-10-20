using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Features;

public sealed record CartItemView(Guid Id, ProductView Product, Dictionary<string, string[]> Selections, string[] Summary, int Quantity, int UnitPrice, int Total, bool Available);
public sealed record CartView(CartItemView[] Items, int Subtotal, int Count, int Revision);
public sealed record CartChange(string ProductId, Dictionary<string, string[]>? Selections, int Quantity = 1);
public sealed record CartEdit(Dictionary<string, string[]>? Selections, int Quantity);

public sealed class CartService(ShopDbContext db)
{
    public async Task<ShoppingCart> GetAsync(string owner, CancellationToken ct = default)
    {
        var cart = await db.Carts.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Owner == owner, ct);
        if (cart is not null) return cart;
        cart = new ShoppingCart { Owner = owner };
        db.Carts.Add(cart);
        await db.SaveChangesAsync(ct);
        return cart;
    }
    public async Task<CartView> ViewAsync(string owner, CancellationToken ct = default)
    {
        var cart = await GetAsync(owner, ct);
        var ids = cart.Lines.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Include(x=>x.Category).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var items = new List<CartItemView>();
        foreach (var line in cart.Lines.OrderBy(x => x.Id))
        {
            if (!products.TryGetValue(line.ProductId, out var product)) continue;
            var view = product.View();
            try
            {
                var price = Pricing.Calculate(view, Pricing.Parse(line.Configuration));
                items.Add(new(line.Id, view, price.Selections, price.Summary, line.Quantity, price.UnitPrice, checked(price.UnitPrice * line.Quantity), true));
            }
            catch (ShopException)
            {
                items.Add(new(line.Id, view, Pricing.Parse(line.Configuration), ["Настройки или товар недоступны. Удалите позицию или измените её."], line.Quantity, 0, 0, false));
            }
        }
        return new(items.ToArray(), items.Sum(x => x.Total), items.Sum(x => x.Quantity), cart.Revision);
    }
    public async Task AddAsync(string owner, CartChange change, CancellationToken ct = default)
    {
        ValidateQuantity(change.Quantity);
        var p = await db.Products.Include(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==change.ProductId,ct) ?? throw new ShopException("Товар не найден.", 404);
        var config = Pricing.Calculate(p.View(), change.Selections);
        var cart = await GetAsync(owner, ct);
        var line = cart.Lines.FirstOrDefault(x => x.ProductId == p.Id && x.Configuration == config.Canonical);
        if (line is null) { line = new CartLine { ShoppingCartId = cart.Id, ProductId = p.Id, Configuration = config.Canonical, Quantity = change.Quantity }; db.CartLines.Add(line); }
        else { ValidateQuantity(line.Quantity + change.Quantity); line.Quantity += change.Quantity; }
        cart.Revision++;
        await db.SaveChangesAsync(ct);
    }
    public async Task EditAsync(string owner, Guid id, CartEdit edit, CancellationToken ct = default)
    {
        ValidateQuantity(edit.Quantity);
        var cart = await GetAsync(owner, ct);
        var line = cart.Lines.SingleOrDefault(x => x.Id == id) ?? throw new ShopException("Позиция корзины не найдена.", 404);
        var p = await db.Products.Include(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==line.ProductId,ct) ?? throw new ShopException("Товар не найден.", 404);
        var config = Pricing.Calculate(p.View(), edit.Selections ?? Pricing.Parse(line.Configuration));
        var same = cart.Lines.FirstOrDefault(x => x.Id != id && x.ProductId == line.ProductId && x.Configuration == config.Canonical);
        if (same is not null) { ValidateQuantity(same.Quantity + edit.Quantity); same.Quantity += edit.Quantity; db.CartLines.Remove(line); }
        else { line.Configuration = config.Canonical; line.Quantity = edit.Quantity; }
        cart.Revision++;
        await db.SaveChangesAsync(ct);
    }
    public async Task RemoveAsync(string owner, Guid? id, CancellationToken ct = default)
    {
        var cart = await GetAsync(owner, ct);
        var lines = id.HasValue ? cart.Lines.Where(x => x.Id == id).ToArray() : cart.Lines.ToArray();
        if (id.HasValue && lines.Length == 0) throw new ShopException("Позиция корзины не найдена.", 404);
        db.CartLines.RemoveRange(lines);
        cart.Revision++;
        await db.SaveChangesAsync(ct);
    }
    public async Task MergeAsync(string guestOwner, string owner, CancellationToken ct = default)
    {
        var guest = await GetAsync(guestOwner, ct);
        var target = await GetAsync(owner, ct);
        foreach (var line in guest.Lines.ToArray())
        {
            var same = target.Lines.FirstOrDefault(x => x.ProductId == line.ProductId && x.Configuration == line.Configuration);
            if (same is null) { var added = new CartLine { ShoppingCartId = target.Id, ProductId = line.ProductId, Configuration = line.Configuration, Quantity = line.Quantity }; db.CartLines.Add(added); }
            else
            {
                if (same.Quantity + line.Quantity > 99) throw new ShopException("После объединения получилось больше 99 одинаковых товаров. Уменьшите количество или удалите эту позицию гостевой корзины. Обе корзины сохранены.", 409);
                same.Quantity += line.Quantity;
            }
        }
        db.CartLines.RemoveRange(guest.Lines);
        target.Revision++; guest.Revision++;
        var guestFavorites = await db.Favorites.Where(x => x.Owner == guestOwner).ToArrayAsync(ct);
        foreach (var f in guestFavorites)
            if (!await db.Favorites.AnyAsync(x => x.Owner == owner && x.ProductId == f.ProductId, ct)) db.Favorites.Add(new() { Owner = owner, ProductId = f.ProductId });
        db.Favorites.RemoveRange(guestFavorites);
        var oldPrefix="assistant.action.v5."+Inputs.Hash(guestOwner)[..24]+".";var newPrefix="assistant.action.v5."+Inputs.Hash(owner)[..24]+".";
        foreach(var receipt in await db.SiteSettings.Where(x=>x.Key.StartsWith(oldPrefix)).ToArrayAsync(ct))
        {
            var newKey=newPrefix+receipt.Key[oldPrefix.Length..];if(!await db.SiteSettings.AnyAsync(x=>x.Key==newKey,ct))db.SiteSettings.Add(new(){Key=newKey,Json=receipt.Json});
        }
        var guestDraft=await db.CheckoutDrafts.FindAsync([guestOwner],ct);
        if(guestDraft is not null)
        {
            var guestData=System.Text.Json.Nodes.JsonNode.Parse(guestDraft.Json)!.AsObject();var method=guestData["method"]?.GetValue<string>();
            if(method is "pickup" or "delivery")
            {
                var targetDraft=await db.CheckoutDrafts.FindAsync([owner],ct);var data=System.Text.Json.Nodes.JsonNode.Parse(targetDraft?.Json??"{}")!.AsObject();data["method"]=method;
                if(targetDraft is null){targetDraft=new(){Owner=owner};db.CheckoutDrafts.Add(targetDraft);}targetDraft.Json=data.ToJsonString();
            }
        }
        await db.SaveChangesAsync(ct);
    }
    private static void ValidateQuantity(int quantity) { if (quantity is < 1 or > 99) throw new ShopException("Количество должно быть от 1 до 99."); }
}
