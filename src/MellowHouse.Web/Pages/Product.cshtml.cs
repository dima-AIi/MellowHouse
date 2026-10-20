using MellowHouse.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Pages;
public class ProductModel(ShopDbContext db) : PageModel
{
    public Product Product { get; set; } = null!;
    public List<Product> Related { get; set; } = [];
    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct)
    {
        var p = await db.Products.Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id&&!x.Draft&&!x.Archived&&x.Category.Visible&&!x.Category.Archived, ct);
        if(p is null) { Response.StatusCode = 404; return Page(); }
        Product = p; Related = await db.Products.Where(x => x.CategoryId == p.CategoryId && x.Id != id && x.Available&&!x.Draft&&!x.Archived).OrderBy(x => x.SortOrder).Take(4).ToListAsync(ct); return Page();
    }
}
