using MellowHouse.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Pages;
public class IndexModel(ShopDbContext db) : PageModel
{
    public List<Product> Popular { get; set; } = [];
    public List<Category> Categories { get; set; } = [];
    public async Task OnGetAsync(CancellationToken ct) { Popular = await db.Products.Where(x => x.Popular&&x.Available&&!x.Archived&&!x.Draft&&x.Category.Visible&&!x.Category.Archived).OrderBy(x => x.SortOrder).ToListAsync(ct); Categories=await db.Categories.Where(x=>x.Visible&&!x.Archived&&x.OnHome&&x.Image!="").OrderBy(x=>x.SortOrder).ToListAsync(ct); }
}
