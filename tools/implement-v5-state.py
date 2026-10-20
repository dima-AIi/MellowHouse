from pathlib import Path
r=Path(r'D:\дима\MellowHouse\src\MellowHouse.Web')
p=r/'Features/ApiEndpoints.cs';s=p.read_text(encoding='utf-8');needle='Customer? c = h.User.Identity?.IsAuthenticated == true ? await db.Customers.FindAsync([IdentityContext.Customer(h)], ct) : null;';assert needle in s;s=s.replace(needle,needle+'\n            var savedMethod=ReadMethod((await db.CheckoutDrafts.FindAsync([owner],ct))?.Json);');s=s.replace('return Results.Ok(new { csrfToken =','return Results.Ok(new { fulfillmentMethod=savedMethod, csrfToken =',1);idx=s.index('    public sealed record FavoriteRequest');s=s[:idx]+'''    private static string ReadMethod(string? json)
    {
        if(string.IsNullOrWhiteSpace(json))return "pickup";
        using var doc=JsonDocument.Parse(json);return doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("method",out var value)&&value.ValueKind==JsonValueKind.String&&value.GetString()=="delivery"?"delivery":"pickup";
    }
'''+s[idx:];p.write_text(s,encoding='utf-8')
p=r/'wwwroot/js/core.js';s=p.read_text(encoding='utf-8');s=s.replace("app.state=await request('/api/state');updateState();","app.state=await request('/api/state');if(!['pickup','delivery'].includes(localStorage.getItem('mh.method'))&&['pickup','delivery'].includes(app.state.fulfillmentMethod))app.method=app.state.fulfillmentMethod;updateState();");p.write_text(s,encoding='utf-8')
p=r/'Features/CartService.cs';s=p.read_text(encoding='utf-8');idx=s.index('        var guestDraft=',s.index('MergeAsync'));s=s[:idx]+'''        var oldPrefix="assistant.action.v5."+Inputs.Hash(guestOwner)[..24]+".";var newPrefix="assistant.action.v5."+Inputs.Hash(owner)[..24]+".";
        foreach(var receipt in await db.SiteSettings.Where(x=>x.Key.StartsWith(oldPrefix)).ToArrayAsync(ct))
        {
            var newKey=newPrefix+receipt.Key[oldPrefix.Length..];if(!await db.SiteSettings.AnyAsync(x=>x.Key==newKey,ct))db.SiteSettings.Add(new(){Key=newKey,Json=receipt.Json});
        }
'''+s[idx:];p.write_text(s,encoding='utf-8')
p=r/'Features/AdminService.cs';s=p.read_text(encoding='utf-8');s=s.replace('else row.MissingCost=true;','else row.AddUnknown(o.Id,index,quantity);');s=s.replace('public bool MissingCost{get;set;}','private readonly Dictionary<(long,int),int> unknown=[];public bool MissingCost=>unknown.Values.Any(q=>q!=0);public void AddUnknown(long id,int index,int quantity){var key=(id,index);unknown[key]=unknown.GetValueOrDefault(key)+quantity;}');s=s.replace('return new {operatingExpenses,expenses,','return new {expenseCategories=expenses.GroupBy(e=>e.Category).Select(g=>new{category=g.Key,total=g.Sum(e=>(long)e.Amount),count=g.Count()}).ToArray(),operatingExpenses,expenses,');p.write_text(s,encoding='utf-8')
p=r/'Features/SiteService.cs';s=p.read_text(encoding='utf-8');needle='            siteEntry.Json=JsonSerializer.Serialize(settings,JsonDefaults.Options);';assert needle in s;s=s.replace(needle,'''            if(!await db.SiteSettings.AnyAsync(x=>x.Key=="site.contacts.v5",ct))
            {
                if(string.IsNullOrWhiteSpace(settings.Phone))settings.Phone="+70000000000";
                if(string.IsNullOrWhiteSpace(settings.Email))settings.Email="hello@mellowhouse.example";
                if(string.IsNullOrWhiteSpace(settings.Address))settings.Address="Москва, Примерная улица, 12";
                if(string.IsNullOrWhiteSpace(settings.Hours))settings.Hours="Ежедневно 08:00–22:00 (МСК)";
                db.SiteSettings.Add(new(){Key="site.contacts.v5",Json="{\\\"example\\\":true}"});
            }
'''+needle);p.write_text(s,encoding='utf-8')
p=r/'wwwroot/js/admin-people.js';s=p.read_text(encoding='utf-8');s=s.replace('Если реальные реквизиты ещё неизвестны, оставьте поле пустым. На сайте будет аккуратное состояние без выдуманных данных.','Контакты используются на сайте и при оформлении. Перед публикацией замените примерные реквизиты на настоящие.');p.write_text(s,encoding='utf-8')
print('Saved fulfillment, login retry receipts, refund-cost fix and example contacts')
