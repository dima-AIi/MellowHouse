from pathlib import Path
r=Path(r'D:\дима\MellowHouse\src\MellowHouse.Web')
def change(f,old,new):
 p=r/f;s=p.read_text(encoding='utf-8');assert old in s,(f,old[:80]);p.write_text(s.replace(old,new),encoding='utf-8')
change('Data/Models.cs','public string? EmailNormalized { get; set; }','public string? BirthDate { get; set; }\n    public string? DeliveryPhone { get; set; }\n    public string? EmailNormalized { get; set; }')
change('Features/ApiEndpoints.cs','c.Contact, c.Role, hasPassword','c.Contact, c.Role, c.BirthDate, c.DeliveryPhone, hasPassword')
change('Features/ApiEndpoints.cs','if(r.SaveAddress)\n','''if(r.SavePersonal)
            {
                if(!string.IsNullOrEmpty(r.BirthDate) && (!DateOnly.TryParseExact(r.BirthDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var birthday)||birthday>DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3))||birthday<DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddYears(-125)))throw new ShopException("Проверьте дату рождения.",field:"birthday");
                if(!string.IsNullOrWhiteSpace(r.DeliveryPhone))Inputs.Phone(r.DeliveryPhone,"phone");
            }
            if(r.SaveAddress)
''')
change('Features/ApiEndpoints.cs','c.Name = r.Name?.Trim() ?? "";','if(r.SavePersonal){c.BirthDate=string.IsNullOrEmpty(r.BirthDate)?null:r.BirthDate;c.DeliveryPhone=string.IsNullOrWhiteSpace(r.DeliveryPhone)?null:Inputs.Phone(r.DeliveryPhone,"phone");}\n            c.Name = r.Name?.Trim() ?? "";')
change('Features/ApiEndpoints.cs','bool SaveAddress=false);','bool SaveAddress=false,string? BirthDate=null,string? DeliveryPhone=null,bool SavePersonal=false);')
# Checkout defaults can use a separate order phone while sign-in contact remains unchanged.
p=r/'wwwroot/js/checkout.js';s=p.read_text(encoding='utf-8');s=s.replace('app.state.profile.contact','(app.state.profile.deliveryPhone||app.state.profile.contact)');p.write_text(s,encoding='utf-8')
# Email is an additional login identifier on the same admin, preserving ID, password hash and order ownership.
p=r/'Features/SiteService.cs';s=p.read_text(encoding='utf-8');s=s.replace('if(settings.Video=="/videos/coffee.mp4")settings.Video="/videos/coffee-full-v4.mp4";','if(settings.Video is "/videos/coffee.mp4" or "/videos/coffee-full-v4.mp4")settings.Video="/videos/coffee-full-v5.mp4";');s=s.replace('= "/videos/coffee-full-v4.mp4"','= "/videos/coffee-full-v5.mp4"');needle='        await db.SaveChangesAsync(ct);\n    }\n}\n';assert needle in s
extra='''        var primary=await db.Customers.Where(x=>x.Role=="Admin").OrderBy(x=>x.CreatedUtc).FirstOrDefaultAsync(ct);
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
''';s=s.replace(needle,extra+needle);p.write_text(s,encoding='utf-8')
# Full list for active orders; completed/rejected orders remain final and paid completion is enforced.
p=r/'Features/OrderService.cs';s=p.read_text(encoding='utf-8');start=s.index('        var allowed=o.Fulfillment switch');end=s.index('        if(r.Status!="Rejected"',start);s=s[:start]+'''        var allowed=new[]{"New","Accepted","Preparing","Ready","OutForDelivery","Completed","Rejected"};
        if(!allowed.Contains(r.Status)||o.Fulfillment is "Completed" or "Rejected")throw new ShopException("Заказ завершён либо указан неизвестный статус.",409);
        if(r.Status=="OutForDelivery"&&checkout.Method!="delivery")throw new ShopException("Самовывоз не передают курьеру.",409);
'''+s[end:];p.write_text(s,encoding='utf-8')
p=r/'wwwroot/js/admin-orders.js';s=p.read_text(encoding='utf-8');start=s.index('const next={New:');end=s.index(';const dlg=dialog',start);s=s[:start]+"const next=['Completed','Rejected'].includes(o.fulfillment)?[]:['New','Accepted','Preparing','Ready',...(o.checkout.method==='delivery'?['OutForDelivery']:[]),'Completed','Rejected']"+s[end:];s=s.replace("select('Следующий статус','nextStatus',next.map(s=>[s,status(s)]),next[0])","select('Статус заказа','nextStatus',next.map(s=>[s,status(s)]),o.fulfillment)");p.write_text(s,encoding='utf-8')
print('Profile, email and status backend saved')
