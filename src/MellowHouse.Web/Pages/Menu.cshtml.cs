using MellowHouse.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Pages;
public class MenuModel(ShopDbContext db) : PageModel
{
    public List<Category> Categories { get; set; } = [];
    public string Tab { get; set; } = "menu";
    public async Task OnGetAsync(string? tab, CancellationToken ct) { Tab = tab is "recommended" or "favorites" ? tab : "menu"; Categories = await db.Categories.Where(x=>x.Visible&&!x.Archived).Include(x => x.Products.Where(p => !p.Archived&&!p.Draft).OrderBy(p => p.SortOrder)).OrderBy(x => x.SortOrder).ToListAsync(ct); }
}
