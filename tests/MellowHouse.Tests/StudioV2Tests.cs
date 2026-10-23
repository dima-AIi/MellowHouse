using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MellowHouse.Web.Data;
using MellowHouse.Web.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StbImageSharp;
using Xunit;
using ZXing;
using ZXing.Common;

namespace MellowHouse.Tests;

public sealed class StudioV2Tests
{
    static string Id(JsonNode n)=>n["id"]!.GetValue<string>();
    static int Number(JsonNode n,string key)=>n[key]!.GetValue<int>();
    static async Task Admin(Buyer b,ShopFactory f)
    {
        await b.State();
        var access=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(f.DataPath,"admin-access.json")))!;
        var r=await b.Send("/api/auth/admin",new{login=access["login"]!.GetValue<string>(),password=access["password"]!.GetValue<string>()},"POST");
        Assert.Equal(HttpStatusCode.OK,r.Status);await b.State();
    }
    static JsonNode Product(JsonNode catalog)=>catalog["products"]!.AsArray().First()!.DeepClone();
    static async Task Save(Buyer admin,JsonNode p) { var r=await admin.Send("/api/admin/products/"+Id(p),p,"PUT");Assert.Equal(HttpStatusCode.OK,r.Status); }
    [Fact] public async Task Roles_RejectGuestAndCustomer_AndLastAdminCannotBeBlocked()
    {
        using var f=new ShopFactory();using var guest=new Buyer(f);await guest.State();
        foreach(var route in new[]{"catalog","orders","clients","settings","audit","report"})Assert.Equal(HttpStatusCode.Unauthorized,(await guest.Send("/api/admin/"+route)).Status);
        Assert.Equal(HttpStatusCode.Redirect,(await guest.Send("/Admin/Catalog")).Status);
        await guest.SignIn();Assert.Equal(HttpStatusCode.Forbidden,(await guest.Send("/api/admin/orders")).Status);
        Assert.Equal(HttpStatusCode.Forbidden,(await guest.Send("/api/admin/categories",new{name="No",sortOrder=1,visible=true,image="",onHome=false},"POST")).Status);
        using var admin=new Buyer(f);await Admin(admin,f);var state=await admin.State();Assert.Equal("Admin",state["profile"]!["role"]!.GetValue<string>());
        var self=Id(state["profile"]!);Assert.Equal(HttpStatusCode.BadRequest,(await admin.Send($"/api/admin/clients/{self}/block",new{blocked=true,reason="Test"},"PUT")).Status);
    }
    [Fact] public async Task EmailPassword_IsHashed_LegacyProfileCannotBeClaimed_AndBlockInvalidatesSession()
    {
        using var f=new ShopFactory();using var customer=new Buyer(f);await customer.SignIn("buyer@example.test");var id=Id((await customer.State())["profile"]!);
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();var c=await db.Customers.FindAsync(Guid.Parse(id));Assert.NotEqual("TestMellow2026!",c!.PasswordHash);Assert.StartsWith("AQ",c.PasswordHash);db.Customers.Add(new(){LoginKey="old-email",Contact="old@example.test",EmailNormalized="old@example.test"});await db.SaveChangesAsync();}
        using var another=new Buyer(f);await another.State();
        Assert.Equal(HttpStatusCode.BadRequest,(await another.Send("/api/auth/login",new{email="buyer@example.test",password="wrong"},"POST")).Status);
        Assert.Equal(HttpStatusCode.BadRequest,(await another.Send("/api/auth/register",new{email="old@example.test",password="NewMellow2026!",name="Чужой"},"POST")).Status);
        using var admin=new Buyer(f);await Admin(admin,f);
        Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/clients/{id}/block",new{blocked=true,reason="Тест блокировки"},"PUT")).Status);
        Assert.False((await customer.State())["authenticated"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden,(await another.Send("/api/auth/login",new{email="buyer@example.test",password="TestMellow2026!"},"POST")).Status);
        await admin.Send($"/api/admin/clients/{id}/block",new{blocked=false},"PUT");
        Assert.Equal(HttpStatusCode.OK,(await another.Send("/api/auth/login",new{email="buyer@example.test",password="TestMellow2026!"},"POST")).Status);
    }
    [Fact] public async Task Reset_IsSingleUse_Expires_RejectsOldPasswordAndSession_AndKeepsOrders()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn("reset@example.test");await b.Add("bbq-chicken");var order=await b.Checkout();var id=Id((await b.State())["profile"]!);
        using var admin=new Buyer(f);await Admin(admin,f);var reset=await admin.Send($"/api/admin/clients/{id}/reset",new{},"POST");var token=reset.Json["url"]!.GetValue<string>().Split('#')[1];
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();Assert.DoesNotContain(token,(await db.PasswordResets.SingleAsync()).TokenHash);}
        using var fresh=new Buyer(f);await fresh.State();
        Assert.Equal(HttpStatusCode.OK,(await fresh.Send("/api/auth/reset",new{token,password="ChangedMellow2026!"},"POST")).Status);await fresh.State();
        Assert.Equal(HttpStatusCode.BadRequest,(await fresh.Send("/api/auth/reset",new{token,password="OtherMellow2026!"},"POST")).Status);
        Assert.False((await b.State())["authenticated"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest,(await fresh.Send("/api/auth/login",new{email="reset@example.test",password="TestMellow2026!"},"POST")).Status);
        Assert.Equal(HttpStatusCode.OK,(await fresh.Send("/api/auth/login",new{email="reset@example.test",password="ChangedMellow2026!"},"POST")).Status);await fresh.State();
        Assert.Equal(HttpStatusCode.OK,(await fresh.Send("/api/orders/"+Id(order))).Status);
        reset=await admin.Send($"/api/admin/clients/{id}/reset",new{},"POST");token=reset.Json["url"]!.GetValue<string>().Split('#')[1];
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();var r=await db.PasswordResets.SingleAsync(x=>!x.Consumed);r.ExpiresUtc=DateTime.UtcNow.AddSeconds(-1);await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.BadRequest,(await fresh.Send("/api/auth/reset",new{token,password="ExpireMellow2026!"},"POST")).Status);
        using var phone=new Buyer(f);await phone.SignIn("+79990000123");var phoneId=Id((await phone.State())["profile"]!);Assert.Equal(HttpStatusCode.BadRequest,(await admin.Send($"/api/admin/clients/{phoneId}/reset",new{},"POST")).Status);
    }
    [Fact] public void Phone_IsStrictAndNormalizesRussianPrefix()
    {
        Assert.Equal("79991234567",Inputs.Phone("8 (999) 123-45-67"));Assert.Equal("79991234567",Inputs.Phone("+7 (999) 123-45-67"));
        foreach(var value in new[]{"555555555555","799912345678","+7 999 abc 1234567","7999","9991234567"})Assert.Throws<ShopException>(()=>Inputs.Phone(value));
    }
    [Fact] public async Task GenericCatalog_CreateOptionsTransferArchiveAndRestart_DoesNotRewriteDatabase()
    {
        string path,id,category,originalName;
        using(var f=new ShopFactory()){
            path=f.DataPath;using var a=new Buyer(f);await Admin(a,f);var catalog=(await a.Send("/api/admin/catalog")).Json;var p=Product(catalog);originalName=p["name"]!.GetValue<string>();
            var c=await a.Send("/api/admin/categories",new{name="Авторские вкусы",sortOrder=11,visible=true,image="",onHome=false},"POST");category=Id(c.Json);
            p["name"]="Новый напиток";p["categoryId"]=category;p["cost"]=80;p["groups"]=JsonSerializer.SerializeToNode(new[]{new OptionGroup("topping","Топпинг",[new("cinnamon","Корица",20),new("cocoa","Какао",30)],[],true,false,0,1)},JsonDefaults.Options);
            var r=await a.Send("/api/admin/products",p,"POST");Assert.Equal(HttpStatusCode.OK,r.Status);id=Id(r.Json);
            using var b=new Buyer(f);await b.State();Assert.Equal(HttpStatusCode.OK,(await b.Send("/api/cart",new{productId=id,quantity=1,selections=new{topping=new[]{"cinnamon"}}},"POST")).Status);
            Assert.Equal(HttpStatusCode.BadRequest,(await b.Send("/api/cart",new{productId=id,quantity=1,selections=new{topping=new[]{"cinnamon","cocoa"}}},"POST")).Status);
            var publicProduct=(await b.Send("/api/products/"+id)).Json;Assert.Null(publicProduct["cost"]);
            Assert.Equal(HttpStatusCode.BadRequest,(await a.Send("/api/admin/categories/"+category,new{withProducts=false},"DELETE")).Status);
            Assert.Equal(HttpStatusCode.OK,(await a.Send("/api/admin/categories/"+category,new{transferTo="coffee",withProducts=false},"DELETE")).Status);
            Assert.Equal("coffee",(await b.Send("/api/products/"+id)).Json["categoryId"]!.GetValue<string>());
            await a.Send("/api/admin/products/"+id,method:"DELETE");Assert.Equal(HttpStatusCode.NotFound,(await b.Send("/api/products/"+id)).Status);
            await a.Send("/api/admin/products/"+id+"/restore",new{},"POST");p["name"]="Имя после перезапуска";p["categoryId"]="coffee";p["id"]=id;await Save(a,p);
            var old=Product(catalog);old["name"]=originalName+" · изменение";await Save(a,old);
        }
        using(var f=new ShopFactory(path)){using var b=new Buyer(f);Assert.Equal("Имя после перезапуска",(await b.Send("/api/products/"+id)).Json["name"]!.GetValue<string>());Assert.DoesNotContain((await b.Send("/api/catalog")).Json["categories"]!.AsArray(),x=>Id(x!)==category);}
    }
    [Fact] public async Task ReceiptFulfillment_RequiresPaymentBeforeCompletion_AndKeepsClientInformed()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("bbq-chicken");var o=await b.Checkout("pickup","cash");var id=Id(o);Assert.Equal(0,Number((await b.State())["cart"]!,"subtotal"));
        using var a=new Buyer(f);await Admin(a,f);
        Assert.Equal(HttpStatusCode.Conflict,(await a.Send($"/api/admin/orders/{id}/status",new{status="Completed"},"PUT")).Status);
        foreach(var status in new[]{"Accepted","Preparing","Ready"})Assert.Equal(HttpStatusCode.OK,(await a.Send($"/api/admin/orders/{id}/status",new{status},"PUT")).Status);
        Assert.Equal("Ready",(await b.Send("/api/orders/"+id)).Json["fulfillment"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict,(await a.Send($"/api/admin/orders/{id}/status",new{status="Completed"},"PUT")).Status);
        var payment=new{requestKey=Guid.NewGuid(),method="terminal"};await a.Send($"/api/admin/orders/{id}/receipt",payment,"POST");await a.Send($"/api/admin/orders/{id}/receipt",payment,"POST");
        Assert.Equal(HttpStatusCode.OK,(await a.Send($"/api/admin/orders/{id}/status",new{status="Completed"},"PUT")).Status);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();Assert.Equal(1,await db.PaymentAttempts.CountAsync());Assert.Equal(1,await db.OrderEvents.CountAsync(x=>x.Status=="Completed"));
    }
    [Fact] public async Task ManualOrder_UsesCurrentPricing_DoesNotCreateCustomer_AndRejectDoesNotRefund()
    {
        using var f=new ShopFactory();using var a=new Buyer(f);await Admin(a,f);
        var payload=new{checkout=Buyer.Details("delivery","cash"),lines=new[]{new{productId="bbq-chicken",quantity=2}},customerId=(Guid?)null};
        var r=await a.Send("/api/admin/orders",payload,"POST");Assert.Equal(HttpStatusCode.OK,r.Status);Assert.Equal(970,Number(r.Json,"total"));var id=Id(r.Json);
        Assert.Equal(id,Id((await a.Send("/api/admin/orders",payload,"POST")).Json));
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Send($"/api/admin/orders/{id}/status",new{status="Rejected",reason=""},"PUT")).Status);
        await a.Send($"/api/admin/orders/{id}/receipt",new{requestKey=Guid.NewGuid(),method="cash"},"POST");
        await a.Send($"/api/admin/orders/{id}/status",new{status="Rejected",reason="Тест отказа"},"PUT");
        var o=(await a.Send("/api/admin/orders/"+id)).Json["order"]!;Assert.Equal("Paid",o["status"]!.GetValue<string>());Assert.Equal(0,Number(o,"refunded"));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();Assert.Equal(1,await db.Customers.CountAsync());Assert.Equal(Guid.Empty,(await db.Orders.SingleAsync()).CustomerId);
    }
    [Fact] public async Task Report_UsesPaidOperations_HistoricalCosts_RefundsAndSeparateDelivery()
    {
        using var f=new ShopFactory();using var a=new Buyer(f);await Admin(a,f);var catalog=(await a.Send("/api/admin/catalog")).Json;var p=catalog["products"]!.AsArray().Single(x=>Id(x!)=="bbq-chicken")!.DeepClone();p["cost"]=100;p["name"]="=SUM(1+1)";await Save(a,p);
        using var b=new Buyer(f);await b.SignIn();await b.Add("bbq-chicken",quantity:2);var o=await b.Checkout();var id=Id(o);
        var report=(await a.Send("/api/admin/report")).Json;Assert.Equal(0,Number(report,"gross"));
        await b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="cancel"},"POST");await b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="error"},"POST");await b.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");
        p["cost"]=999;p["price"]=999;await Save(a,p);
        var refund=new{requestKey=Guid.NewGuid(),reason="Тест возврата",lines=new[]{new{index=0,quantity=1}},delivery=false};await a.Send($"/api/admin/orders/{id}/refund",refund,"POST");await a.Send($"/api/admin/orders/{id}/refund",refund,"POST");
        report=(await a.Send("/api/admin/report")).Json;Assert.Equal(970,Number(report,"gross"));Assert.Equal(390,Number(report,"refunds"));Assert.Equal(580,Number(report,"collected"));Assert.Equal(390,Number(report,"productRevenue"));Assert.Equal(190,Number(report,"delivery"));Assert.Equal(100,Number(report,"cost"));Assert.Equal(290,Number(report,"margin"));Assert.Equal(1,Number(report,"paidOrders"));
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Send($"/api/admin/orders/{id}/refund",new{requestKey=Guid.NewGuid(),reason="Слишком много",lines=new[]{new{index=0,quantity=2}},delivery=false},"POST")).Status);
        var csv=await a.Raw("/api/admin/report.csv");Assert.Equal(HttpStatusCode.OK,csv.Status);Assert.Equal(new byte[]{239,187,191},csv.Bytes.Take(3));Assert.Contains("\"'=SUM(1+1)\"",Encoding.UTF8.GetString(csv.Bytes));
        await a.Send($"/api/admin/orders/{id}/refund",new{requestKey=Guid.NewGuid(),reason="Остаток",lines=new[]{new{index=0,quantity=1}},delivery=true},"POST");
        report=(await a.Send("/api/admin/report")).Json;Assert.Equal(0,Number(report,"collected"));Assert.Null(report["marginPercent"]);
        await b.Add("vanilla-raf");o=await b.Checkout("pickup");await b.Send($"/api/orders/{Id(o)}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");report=(await a.Send("/api/admin/report")).Json;Assert.True(report["missingCost"]!.GetValue<bool>());Assert.Null(report["cost"]);Assert.Null(report["margin"]);
    }
    [Fact] public async Task Qr_IsDecodablePlainText_AndPrivate()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.SignIn();await b.Add("vanilla-raf");var o=await b.Checkout("pickup","sbp");var id=Id(o);var qr=await b.Raw($"/api/orders/{id}/qr");Assert.Equal(HttpStatusCode.OK,qr.Status);
        var pixels=ImageResult.FromMemory(qr.Bytes,ColorComponents.RedGreenBlueAlpha);var reader=new BarcodeReaderGeneric();reader.Options.TryHarder=true;reader.Options.PureBarcode=true;var decoded=reader.Decode(pixels.Data,pixels.Width,pixels.Height,RGBLuminanceSource.BitmapFormat.RGBA32);
        Assert.NotNull(decoded);Assert.Equal("MELLOWHOUSE:ORDER:"+Guid.Parse(id).ToString("N")+":TEST",decoded.Text);Assert.DoesNotContain("http",decoded.Text,StringComparison.OrdinalIgnoreCase);
        using var c=new Buyer(f);await c.SignIn();Assert.Equal(HttpStatusCode.NotFound,(await c.Raw($"/api/orders/{id}/qr")).Status);
    }
    [Fact] public void GeneratedQr_RemainsReadableAcrossDifferentPayloadPatterns()
    {
        var reader=new BarcodeReaderGeneric();reader.Options.TryHarder=true;reader.Options.PureBarcode=true;
        for(var i=0;i<64;i++)
        {
            var payload="MELLOWHOUSE:ORDER:"+Guid.NewGuid().ToString("N")+":TEST";
            var png=QRCoder.PngByteQRCodeHelper.GetQRCode(payload,QRCoder.QRCodeGenerator.ECCLevel.Q,6);
            var pixels=ImageResult.FromMemory(png,ColorComponents.RedGreenBlueAlpha);
            var decoded=reader.Decode(pixels.Data,pixels.Width,pixels.Height,RGBLuminanceSource.BitmapFormat.RGBA32);
            Assert.NotNull(decoded);Assert.Equal(payload,decoded.Text);
        }
    }
    [Fact] public async Task Assistant_OnlySuggestsLiveCatalog_AndHonorsBudgetAndArchive()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();var r=await b.Send("/api/assistant",new{message="Хочу кофе до 350"},"POST");Assert.Equal(HttpStatusCode.OK,r.Status);Assert.Contains("vanilla-raf",r.Json["productIds"]!.AsArray().Select(x=>x!.GetValue<string>()));
        using var a=new Buyer(f);await Admin(a,f);await a.Send("/api/admin/products/vanilla-raf",method:"DELETE");
        r=await b.Send("/api/assistant",new{message="Хочу кофе"},"POST");Assert.DoesNotContain("vanilla-raf",r.Json["productIds"]!.AsArray().Select(x=>x!.GetValue<string>()));
        Assert.DoesNotContain("API",r.Json["text"]!.GetValue<string>());
    }

    [Fact] public async Task UploadedProductPhoto_RequiresRealAlphaAndDecodableSafeRaster()
    {
        using var f=new ShopFactory();using var a=new Buyer(f);await Admin(a,f);var fixtures=Path.Combine(ShopFactory.FindRoot(),"tests/MellowHouse.Tests/Fixtures");
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Upload(await File.ReadAllBytesAsync(Path.Combine(fixtures,"opaque.png")),"background.png")).Status);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Upload(Encoding.UTF8.GetBytes("<svg onload='alert(1)'>not an image</svg>"),"photo.png")).Status);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Upload(new byte[]{137,80,78,71,13,10,26,10}.Concat(new byte[100]).ToArray(),"broken.png")).Status);
        var valid=await a.Upload(await File.ReadAllBytesAsync(Path.Combine(fixtures,"alpha.png")),"../../escape.png");Assert.Equal(HttpStatusCode.OK,valid.Status);
        var saved=valid.Json["path"]!.GetValue<string>();Assert.StartsWith("/media/",saved);Assert.DoesNotContain("escape",saved);Assert.DoesNotContain("..",saved);
        var download=await a.Raw(saved);Assert.Equal(HttpStatusCode.OK,download.Status);Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(fixtures,"alpha.png")),download.Bytes);
    }

}
