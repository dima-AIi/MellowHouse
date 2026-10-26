using System.Text.Json;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Features;

public sealed record BrowserCartLine(string ProductId,Dictionary<string,string[]>? Selections,int Quantity,int? ExpectedUnitPrice=null);
public sealed record BrowserCartRequest(string RequestKey,BrowserCartLine[] Lines);
public sealed record BrowserCartResult(CartView Cart,string[] Warnings);
public sealed record BrowserCartReceipt(string Fingerprint,string[] Warnings);

/// <summary>Merges a browser draft once, even if the response is lost and the request is repeated.</summary>
public sealed class BrowserCartSync(ShopDbContext db,CartService carts)
{
    public async Task<BrowserCartResult> MergeAsync(string owner,BrowserCartRequest request,CancellationToken ct)
    {
        OrderService.ValidateKey(request.RequestKey);
        if(request.Lines is null||request.Lines.Length is <1 or >50||request.Lines.Any(l=>l is null||string.IsNullOrWhiteSpace(l.ProductId)||l.ProductId.Length>100||l.Quantity is <1 or >99))
            throw new ShopException("Проверьте сохранённую корзину: до 50 позиций, количество от 1 до 99.");
        var key="cart.browser.v17."+Inputs.Hash(owner)[..24]+"."+Guid.Parse(request.RequestKey).ToString("N");
        var fingerprint=Inputs.Hash(JsonSerializer.Serialize(request.Lines,JsonDefaults.Options));
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var previous=await db.SiteSettings.FindAsync([key],ct);
        if(previous is not null)
        {
            var receipt=JsonSerializer.Deserialize<BrowserCartReceipt>(previous.Json,JsonDefaults.Options)!;
            if(receipt.Fingerprint!=fingerprint)throw new ShopException("Эта корзина уже синхронизирована с другим составом. Обновите страницу и проверьте позиции.",409);
            var current=await carts.ViewAsync(owner,ct);await tx.CommitAsync(ct);return new(current,receipt.Warnings);
        }
        var warnings=new List<string>();
        foreach(var line in request.Lines)
        {
            var p=await db.Products.Include(p=>p.Category).SingleOrDefaultAsync(p=>p.Id==line.ProductId,ct);
            if(p is null||p.Archived||p.Draft||p.Category.Archived||!p.Category.Visible){warnings.Add("Товар больше не входит в меню и не добавлен в корзину.");continue;}
            try
            {
                var config=Pricing.Calculate(p.View(),line.Selections);
                var cart=await carts.GetAsync(owner,ct);
                var existing=cart.Lines.FirstOrDefault(l=>l.ProductId==p.Id&&l.Configuration==config.Canonical)?.Quantity??0;
                var quantity=Math.Min(line.Quantity,99-existing);
                if(quantity>0)await carts.AddAsync(owner,new(p.Id,config.Selections,quantity),ct);
                if(quantity!=line.Quantity)warnings.Add(p.Name+": количество ограничено 99 порциями с учётом сохранённой корзины.");
                if(line.ExpectedUnitPrice.HasValue&&line.ExpectedUnitPrice.Value!=config.UnitPrice)warnings.Add(p.Name+": цена обновилась. Проверьте итоговую сумму.");
            }
            catch(ShopException error){warnings.Add(p.Name+": "+error.Message+" Позиция не добавлена.");}
        }
        var result=new BrowserCartResult(await carts.ViewAsync(owner,ct),warnings.Distinct().ToArray());
        db.SiteSettings.Add(new(){Key=key,Json=JsonSerializer.Serialize(new BrowserCartReceipt(fingerprint,result.Warnings),JsonDefaults.Options)});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return result;
    }
}
