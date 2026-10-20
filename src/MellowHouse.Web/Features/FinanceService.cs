using System.Globalization;
using System.Text;
using System.Text.Json;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace MellowHouse.Web.Features;

public sealed record Expense(Guid Id,string Date,string Category,int Amount,string Note,bool Archived=false,string Source="Staff");
public sealed record ExpenseInput(string Date,string Category,int Amount,string? Note);
public sealed class FinanceService(ShopDbContext db)
{
    public const string Key="finance.expenses.v1";
    public static readonly string[] Categories=["rent","payroll","utilities","marketing","delivery","other"];
    public async Task<Expense[]> AllAsync(CancellationToken ct)=>JsonSerializer.Deserialize<Expense[]>((await db.SiteSettings.FindAsync([Key],ct))?.Json??"[]",JsonDefaults.Options)??[];
    public async Task<Expense> SaveAsync(Guid? id,ExpenseInput r,Guid actor,CancellationToken ct)
    {
        var today=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        if(!DateOnly.TryParseExact(r.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||date>today||date<today.AddYears(-10))throw new ShopException("Укажите дату расхода не позднее сегодняшней.");
        if(!Categories.Contains(r.Category)||r.Amount is <1 or >100000000||(r.Note??"").Length>300)throw new ShopException("Проверьте категорию, сумму (1–100 000 000 ₽) и комментарий.");
        var all=(await AllAsync(ct)).ToList();var index=id.HasValue?all.FindIndex(x=>x.Id==id.Value):-1;
        if(id.HasValue&&(index<0||all[index].Archived))throw new ShopException("Расход не найден.",404);
        var expense=new Expense(id??Guid.NewGuid(),r.Date,r.Category,r.Amount,(r.Note??"").Trim());
        if(index<0)all.Add(expense);else all[index]=expense;
        await StoreAsync(all,actor,id.HasValue?"expense.updated":"expense.created",expense.Id,ct);return expense;
    }
    public async Task ArchiveAsync(Guid id,Guid actor,CancellationToken ct)
    {
        var all=(await AllAsync(ct)).ToList();var index=all.FindIndex(x=>x.Id==id);if(index<0)throw new ShopException("Расход не найден.",404);
        if(all[index].Archived)return;all[index]=all[index] with{Archived=true};await StoreAsync(all,actor,"expense.archived",id,ct);
    }
    private async Task StoreAsync(List<Expense> all,Guid actor,string action,Guid id,CancellationToken ct)
    {
        var row=await db.SiteSettings.FindAsync([Key],ct);if(row is null){row=new(){Key=Key};db.SiteSettings.Add(row);}row.Json=JsonSerializer.Serialize(all,JsonDefaults.Options);Audit.Log(db,actor,action,id.ToString());await db.SaveChangesAsync(ct);
    }
    public async Task<Expense[]> PeriodAsync(string from,string to,CancellationToken ct)=>(await AllAsync(ct)).Where(x=>!x.Archived&&string.CompareOrdinal(x.Date,from)>=0&&string.CompareOrdinal(x.Date,to)<=0).OrderByDescending(x=>x.Date).ToArray();
}
