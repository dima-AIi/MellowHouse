using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using MellowHouse.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Features;

public sealed record FaqItem(string Question, string Answer);
public sealed class CafeSettings
{
    public string Name { get; set; } = "Mellow House";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Hours { get; set; } = "";
    public string CafeName { get; set; } = "Mellow House";
    public int DeliveryPrice { get; set; } = 190;
    public string[] PaymentMethods { get; set; } = ["card", "sbp", "yoomoney", "cash", "terminal"];
    public string Video { get; set; } = "/videos/coffee-full-v5.mp4";
    public string Poster { get; set; } = "/images/coffee-final-v4.webp";
    public string MarketingTitle { get; set; } = "Ваш кофе. Ваша пауза.";
    public string MenuTitle { get; set; } = "Найдите свой любимый вкус";
    public string MenuText { get; set; } = "Кофе по вашему вкусу, горячие сэндвичи и время для себя. Выберите любимое — остальное мы берём на себя.";
    public FaqItem[] Faq { get; set; } = [new("Можно настроить напиток?", "Да. Выберите объём, молоко, сладость и дополнительный эспрессо в карточке напитка."), new("Можно заказать к определённому времени?", "Да. При оформлении выберите «Ко времени» и укажите доступное время. Следить за статусом можно в истории заказов."), new("Что выбрать, если я не пью обычное молоко?", "В карточке напитка посмотрите доступные виды молока. Состав и аллергены указаны там же. При аллергии учитывайте возможный контакт с другими продуктами на общем оборудовании."), new("Как повторить любимый заказ?", "Откройте историю заказов и нажмите «Повторить». Актуальная цена рассчитывается заново.")];
}
public sealed class SiteService(ShopDbContext db, MediaService media)
{
    public async Task<CafeSettings> GetAsync(CancellationToken ct = default) => JsonSerializer.Deserialize<CafeSettings>((await db.SiteSettings.FindAsync(["site"], ct))?.Json ?? "{}", JsonDefaults.Options) ?? new();
    public async Task SaveAsync(CafeSettings s, Guid actor, CancellationToken ct)
    {
        if(s.PaymentMethods is null||s.Faq is null||s.Faq.Any(x=>x is null)||new[]{s.Phone,s.Email,s.Address,s.Hours,s.MarketingTitle,s.MenuTitle,s.MenuText,s.Video,s.Poster}.Any(x=>x is null))throw new ShopException("Заполните поля настроек; для неизвестных данных оставьте пустую строку.");
        s.Name = Inputs.Required(s.Name, "название", 80); s.CafeName = Inputs.Required(s.CafeName, "название кафе", 100);
        if(s.DeliveryPrice is < 0 or > 10000) throw new ShopException("Стоимость доставки: от 0 до 10 000 ₽.");
        if(s.Phone.Length > 0) s.Phone = "+" + Inputs.Phone(s.Phone);
        if(s.Email.Length > 0) s.Email = Inputs.Email(s.Email);
        if(s.Address.Length > 250 || s.Hours.Length > 250 || s.MarketingTitle.Length > 150 || s.MenuTitle.Length > 150 || s.MenuText.Length > 600) throw new ShopException("Сократите текст настройки.");
        if(s.PaymentMethods.Length==0 || s.PaymentMethods.Length>5 || s.PaymentMethods.Any(x=> !PaymentMethods.All.Contains(x))) throw new ShopException("Выберите доступные способы оплаты.");
        if(s.Faq.Length>12 || s.Faq.Any(x=>string.IsNullOrWhiteSpace(x.Question)||x.Question.Length>150||string.IsNullOrWhiteSpace(x.Answer)||x.Answer.Length>800)) throw new ShopException("FAQ: до 12 вопросов, ответ до 800 символов.");
        if(!MediaService.SafePath(s.Poster,"/images/","/uploads/") || (!string.IsNullOrEmpty(s.Video) && (!MediaService.SafePath(s.Video,"/videos/") || !new[]{".mp4",".webm"}.Contains(Path.GetExtension(s.Video))))) throw new ShopException("Укажите локальные медиа проекта.");
        if(!media.AssetExists(s.Poster)||(!string.IsNullOrEmpty(s.Video)&&!media.AssetExists(s.Video)))throw new ShopException("Файл ролика или poster не найден в проекте.");
        var entry=await db.SiteSettings.FindAsync(["site"],ct); if(entry is null) { entry=new(){Key="site"};db.SiteSettings.Add(entry); }
        entry.Json=JsonSerializer.Serialize(s,JsonDefaults.Options); db.AuditEntries.Add(new(){ActorId=actor,Action="settings.updated",Target="site"});await db.SaveChangesAsync(ct);
    }
}
public static class PaymentMethods
{
    public static readonly string[] All=["card","sbp","yoomoney","cash","terminal"];
    public static bool AtPickup(string method)=>method is "cash" or "terminal";
    public static string Label(string method)=>method switch {"card"=>"Банковская карта","sbp"=>"СБП","yoomoney"=>"ЮMoney","cash"=>"Наличные при получении","terminal"=>"Картой при получении",_=>method};
}
public static class Bootstrap
{
    public static async Task InitializeAsync(IServiceProvider services, IWebHostEnvironment env, IConfiguration config, CancellationToken ct=default)
    {
        var db=services.GetRequiredService<ShopDbContext>();
        if(!await db.SiteSettings.AnyAsync(x=>x.Key=="site",ct)) db.SiteSettings.Add(new(){Key="site",Json=JsonSerializer.Serialize(new CafeSettings(),JsonDefaults.Options)});
        var siteEntry=await db.SiteSettings.FindAsync(["site"],ct);
        if(siteEntry is not null)
        {
            var settings=JsonSerializer.Deserialize<CafeSettings>(siteEntry.Json,JsonDefaults.Options)!;
            if(settings.Video is "/videos/coffee.mp4" or "/videos/coffee-full-v4.mp4")settings.Video="/videos/coffee-full-v5.mp4";
            if(settings.Poster=="/images/coffee-video-poster.webp")settings.Poster="/images/coffee-final-v4.webp";
            if(!await db.SiteSettings.AnyAsync(x=>x.Key=="site.contacts.v5",ct))
            {
                if(string.IsNullOrWhiteSpace(settings.Phone))settings.Phone="+70000000000";
                if(string.IsNullOrWhiteSpace(settings.Email))settings.Email="hello@mellowhouse.example";
                if(string.IsNullOrWhiteSpace(settings.Address))settings.Address="Москва, Примерная улица, 12";
                if(string.IsNullOrWhiteSpace(settings.Hours))settings.Hours="Ежедневно 08:00–22:00 (МСК)";
                db.SiteSettings.Add(new(){Key="site.contacts.v5",Json="{\"example\":true}"});
            }
            siteEntry.Json=JsonSerializer.Serialize(settings,JsonDefaults.Options);
        }
        var customers=await db.Customers.ToArrayAsync(ct);var emails=customers.Where(x=>x.EmailNormalized is not null).Select(x=>x.EmailNormalized!).ToHashSet();
        foreach(var c in customers.Where(x=>!x.LoginKey.StartsWith("sample:") && x.EmailNormalized is null && new EmailAddressAttribute().IsValid(x.Contact))) if(emails.Add(c.Contact.Trim().ToLowerInvariant())) c.EmailNormalized=c.Contact.Trim().ToLowerInvariant();
        var login=(config["Admin:Login"]??"mellow-admin").Trim().ToLowerInvariant();
        if(!await db.Customers.AnyAsync(x=>x.Role=="Admin",ct) && (env.IsDevelopment() || !string.IsNullOrWhiteSpace(config["Admin:Password"])))
        {
            var password=config["Admin:Password"]??("Mh-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(10))+"a9"); Inputs.Password(password);
            var admin=new Customer(){LoginKey="admin:"+login,Contact=login,Name="Администратор",Role="Admin"};
            admin.PasswordHash=services.GetRequiredService<IPasswordHasher<Customer>>().HashPassword(admin,password);db.Customers.Add(admin);
            if(env.IsDevelopment()) {var dir=config["Database:DataDirectory"]??Path.Combine(env.ContentRootPath,"App_Data");Directory.CreateDirectory(dir);await File.WriteAllTextAsync(Path.Combine(dir,"admin-access.json"),JsonSerializer.Serialize(new{login,password},JsonDefaults.Options),ct);}
        }
        var primary=db.Customers.Local.FirstOrDefault(x=>x.Role=="Admin")??await db.Customers.Where(x=>x.Role=="Admin").OrderBy(x=>x.CreatedUtc).FirstOrDefaultAsync(ct);
        if(primary is not null && primary.EmailNormalized is null)
        {
            var email=Inputs.Email(config["Admin:Email"]??"admin@mellowhouse.local");
            if(await db.Customers.AnyAsync(x=>x.EmailNormalized==email&&x.Id!=primary.Id,ct))throw new InvalidOperationException("Admin email already belongs to another account.");
            primary.EmailNormalized=email;if(primary.Contact==login||primary.Contact==primary.LoginKey.Replace("admin:",""))primary.Contact=email;
        }
        if(primary is not null && env.IsDevelopment())
        {
            var access=Path.Combine(config["Database:DataDirectory"]??Path.Combine(env.ContentRootPath,"App_Data"),"admin-access.json");
            if(File.Exists(access)){var json=System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(access,ct))!.AsObject();json["email"]=primary.EmailNormalized;await File.WriteAllTextAsync(access,json.ToJsonString(JsonDefaults.Options),ct);}
        }
        await db.SaveChangesAsync(ct);
    }
}
