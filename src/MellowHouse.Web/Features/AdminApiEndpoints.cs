using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
namespace MellowHouse.Web.Features;
public static class AdminApiEndpoints
{
    public static void Map(WebApplication app)
    {
        var g=app.MapGroup("/api/admin").RequireAuthorization("Admin");
        g.MapGet("/catalog",async(CatalogAdminService s,CancellationToken ct)=>Results.Ok(await s.CatalogAsync(ct)));
        g.MapPost("/products",async(ProductInput r,HttpContext h,CatalogAdminService s,CancellationToken ct)=>Results.Ok(new{id=(await s.SaveProductAsync(null,r,IdentityContext.Admin(h),ct)).Id}));
        g.MapPut("/products/{id}",async(string id,ProductInput r,HttpContext h,CatalogAdminService s,CancellationToken ct)=>Results.Ok(new{id=(await s.SaveProductAsync(id,r,IdentityContext.Admin(h),ct)).Id}));
        g.MapDelete("/products/{id}",async(string id,HttpContext h,CatalogAdminService s,CancellationToken ct)=>{await s.ArchiveProductAsync(id,true,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapPost("/products/{id}/restore",async(string id,HttpContext h,CatalogAdminService s,CancellationToken ct)=>{await s.ArchiveProductAsync(id,false,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapPost("/categories",async(CategoryInput r,HttpContext h,CatalogAdminService s,CancellationToken ct)=>Results.Ok(new{id=(await s.SaveCategoryAsync(null,r,IdentityContext.Admin(h),ct)).Id}));
        g.MapPut("/categories/{id}",async(string id,CategoryInput r,HttpContext h,CatalogAdminService s,CancellationToken ct)=>Results.Ok(new{id=(await s.SaveCategoryAsync(id,r,IdentityContext.Admin(h),ct)).Id}));
        g.MapDelete("/categories/{id}",async(string id,[FromBody] ArchiveCategoryRequest r,HttpContext h,CatalogAdminService s,CancellationToken ct)=>{await s.ArchiveCategoryAsync(id,true,r,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapPost("/categories/{id}/restore",async(string id,HttpContext h,CatalogAdminService s,CancellationToken ct)=>{await s.ArchiveCategoryAsync(id,false,new(null,false),IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapPost("/media",async(HttpContext h,MediaService s,CancellationToken ct)=>{var form=await h.Request.ReadFormAsync(ct);var file=form.Files.GetFile("file")??throw new ShopException("Выберите файл.");return Results.Ok(await s.UploadAsync(file,form["kind"]=="product",ct));});
        g.MapGet("/orders",async(AdminService s,CancellationToken ct)=>Results.Ok(await s.OrdersAsync(ct)));
        g.MapGet("/orders/{id:guid}",async(Guid id,OrderService s,ShopDbContext db,CancellationToken ct)=>{var o=await s.AdminFindAsync(id,ct);return Results.Ok(new{order=await s.DetailedAsync(o,ct),attempts=await db.PaymentAttempts.Where(x=>x.OrderId==o.Id).OrderBy(x=>x.CreatedUtc).Select(x=>new{x.Result,x.CreatedUtc}).ToArrayAsync(ct)});});
        g.MapPut("/orders/{id:guid}/status",async(Guid id,TransitionRequest r,HttpContext h,OrderService s,CancellationToken ct)=>{await s.TransitionAsync(id,r,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapPost("/orders/{id:guid}/receipt",async(Guid id,ReceiptRequest r,HttpContext h,OrderService s,CancellationToken ct)=>{await s.ReceiptAsync(id,r,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapPost("/orders/{id:guid}/refund",async(Guid id,RefundRequest r,HttpContext h,OrderService s,CancellationToken ct)=>{await s.RefundAsync(id,r,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapPost("/orders",async(ManualOrderRequest r,HttpContext h,OrderService s,CancellationToken ct)=>Results.Ok(await s.ManualAsync(r,IdentityContext.Admin(h),ct)));
        g.MapGet("/clients",async(AdminService s,CancellationToken ct)=>Results.Ok(await s.ClientsAsync(ct)));
        g.MapGet("/clients/{id:guid}",async(Guid id,AdminService s,CancellationToken ct)=>Results.Ok(await s.ClientAsync(id,ct)));
        g.MapPut("/clients/{id:guid}/block",async(Guid id,BlockRequest r,HttpContext h,AdminService s,CancellationToken ct)=>{await s.BlockAsync(id,r,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapPost("/clients/{id:guid}/reset",async(Guid id,HttpContext h,AdminService s,CancellationToken ct)=>Results.Ok(new{url=await s.ResetAsync(id,IdentityContext.Admin(h),ct)}));
        g.MapPost("/expenses",async(ExpenseInput r,HttpContext h,FinanceService s,CancellationToken ct)=>Results.Ok(await s.SaveAsync(null,r,IdentityContext.Admin(h),ct)));
        g.MapPut("/expenses/{id:guid}",async(Guid id,ExpenseInput r,HttpContext h,FinanceService s,CancellationToken ct)=>Results.Ok(await s.SaveAsync(id,r,IdentityContext.Admin(h),ct)));
        g.MapDelete("/expenses/{id:guid}",async(Guid id,HttpContext h,FinanceService s,CancellationToken ct)=>{await s.ArchiveAsync(id,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapGet("/report",async(string? from,string? to,AdminService s,CancellationToken ct)=>Results.Ok(await s.ReportAsync(from,to,ct)));
        g.MapGet("/finance.csv",async(string? from,string? to,AdminService s,CancellationToken ct)=>Results.File(await s.FinanceCsvAsync(from,to,ct),"text/csv; charset=utf-8","mellow-house-finance.csv"));
        g.MapGet("/report.csv",async(string? from,string? to,AdminService s,CancellationToken ct)=>Results.File(await s.CsvAsync(from,to,ct),"text/csv; charset=utf-8","mellow-house-report.csv"));
        g.MapGet("/settings",async(SiteService s,IConfiguration config,AssistantHealth health,CancellationToken ct)=>Results.Ok(new{site=await s.GetAsync(ct),paymentProvider="local",assistantProvider=string.IsNullOrWhiteSpace(config["Assistant:ApiKey"])?"local":"nvidia",assistantModel=NvidiaMenuAssistant.Models(config).FirstOrDefault(),assistantModels=NvidiaMenuAssistant.Models(config),assistantLastModel=health.LastModel}));
        g.MapGet("/sample-business",async(ShopDbContext db,CancellationToken ct)=>Results.Ok(System.Text.Json.JsonSerializer.Deserialize<SampleBusiness>((await db.SiteSettings.FindAsync([CafeExpansion.Marker],ct))?.Json??"null",JsonDefaults.Options)));
        g.MapPut("/settings",async(CafeSettings r,HttpContext h,SiteService s,CancellationToken ct)=>{await s.SaveAsync(r,IdentityContext.Admin(h),ct);return Results.Ok(new{saved=true});});
        g.MapGet("/audit",async(ShopDbContext db,CancellationToken ct)=>Results.Ok(await db.AuditEntries.OrderByDescending(x=>x.Id).Take(300).Select(x=>new{x.Id,x.Action,x.Target,x.Detail,x.CreatedUtc,actor=x.ActorId}).ToArrayAsync(ct)));
    }
}
