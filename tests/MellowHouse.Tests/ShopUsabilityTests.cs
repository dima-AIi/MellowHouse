using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using MellowHouse.Web.Data;
using MellowHouse.Web.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace MellowHouse.Tests;

public sealed class ShopUsabilityTests
{
    static async Task Admin(Buyer buyer,ShopFactory f){await buyer.State();var access=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(f.DataPath,"admin-access.json")))!;Assert.Equal(HttpStatusCode.OK,(await buyer.Send("/api/auth/login",new{email=access["email"]!.GetValue<string>(),password=access["password"]!.GetValue<string>()},"POST")).Status);await buyer.State();}
    static Task<byte[]> Webp(string fixture){using var image=SKImage.FromEncodedData(Path.Combine(ShopFactory.FindRoot(),"tests/MellowHouse.Tests/Fixtures",fixture));using var encoded=image.Encode(SKEncodedImageFormat.Webp,100);return Task.FromResult(encoded.ToArray());}

    [Fact] public async Task TransparentWebp_CanBePublished_AndSurvivesNewServerWithoutLocalFiles()
    {
        string path,dataPath;byte[] bytes=await Webp("alpha.png");
        using(var f=new ShopFactory())
        {
            dataPath=f.DataPath;using var admin=new Buyer(f);await Admin(admin,f);
            var upload=await admin.Upload(bytes,"cutout.webp");Assert.Equal(HttpStatusCode.OK,upload.Status);path=upload.Json["path"]!.GetValue<string>();Assert.StartsWith("/media/",path);
            var p=(await admin.Send("/api/admin/catalog")).Json["products"]!.AsArray().First()!.DeepClone();p["image"]=path;p["draft"]=false;
            Assert.Equal(HttpStatusCode.OK,(await admin.Send("/api/admin/products/"+p["id"]!.GetValue<string>(),p,"PUT")).Status);
            Assert.Equal(HttpStatusCode.BadRequest,(await admin.Upload(await Webp("opaque.png"),"opaque.webp")).Status);
            Assert.Equal(HttpStatusCode.BadRequest,(await admin.Upload(bytes,"fake.jpg")).Status);
        }
        using(var restarted=new ShopFactory(dataPath)){using var visitor=new Buyer(restarted);await visitor.State();var file=await visitor.Raw(path);Assert.Equal(HttpStatusCode.OK,file.Status);Assert.Equal(bytes,file.Bytes);}
    }

    [Fact] public async Task Existing78Images_CanBeSavedWithoutPngOnlyException()
    {
        using var f=new ShopFactory(samples:true);using var admin=new Buyer(f);await Admin(admin,f);
        await using var scope=f.Services.CreateAsyncScope();var media=scope.ServiceProvider.GetRequiredService<MediaService>();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        foreach(var image in await db.Products.Select(p=>p.Image).ToArrayAsync())await media.ValidateProductAsync(image,default);
    }

    [Fact] public async Task Nutrition_SeedsFullCatalogOnce_PreservesCustomValues_AndValidatesAdminInput()
    {
        using var f=new ShopFactory(samples:true);using var admin=new Buyer(f);await Admin(admin,f);
        var catalog=(await admin.Send("/api/admin/catalog")).Json;Assert.Equal(78,catalog["products"]!.AsArray().Count);
        Assert.All(catalog["products"]!.AsArray(),p=>Assert.NotNull(p!["nutritionFacts"]));
        var syrniki=catalog["products"]!.AsArray().Single(p=>p!["id"]!.GetValue<string>()=="syrniki")!.DeepClone();
        syrniki["nutritionFacts"]=JsonSerializer.SerializeToNode(new NutritionFacts(400,20,16,44,false),JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.OK,(await admin.Send("/api/admin/products/syrniki",syrniki,"PUT")).Status);
        await using var scope=f.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        await NutritionData.SeedAsync(db,Path.Combine(ShopFactory.FindRoot(),"src/MellowHouse.Web"));Assert.Equal(400,NutritionData.Read((await db.Products.FindAsync("syrniki"))!.Nutrition)!.Calories);
        syrniki["nutritionFacts"]!["protein"]=-1;Assert.Equal(HttpStatusCode.BadRequest,(await admin.Send("/api/admin/products/syrniki",syrniki,"PUT")).Status);
        var detail=await admin.Raw("/Product/syrniki");Assert.Contains("КБЖУ на порцию",System.Text.Encoding.UTF8.GetString(detail.Bytes));
    }

    [Fact] public async Task DeliveryStatus_IsReadByCustomerAtEveryStage_AndDoesNotAdvanceOnItsOwn()
    {
        using var f=new ShopFactory();using var buyer=new Buyer(f);await buyer.SignIn();await buyer.Add("vanilla-raf");var created=await buyer.Checkout("delivery","sbp");var id=created["id"]!.GetValue<string>();
        var pending=await buyer.Send("/api/orders/"+id);Assert.Equal("Pending",pending.Json["status"]!.GetValue<string>());Assert.Equal("New",pending.Json["fulfillment"]!.GetValue<string>());
        await buyer.Send($"/api/orders/{id}/payment",new{attemptKey=Guid.NewGuid(),result="success"},"POST");using var admin=new Buyer(f);await Admin(admin,f);
        foreach(var stage in new[]{"Accepted","Preparing","Ready","OutForDelivery","Completed"})
        {
            Assert.Equal(HttpStatusCode.OK,(await admin.Send($"/api/admin/orders/{id}/status",new{status=stage},"PUT")).Status);
            var customer=await buyer.Send("/api/orders/"+id);Assert.Equal(stage,customer.Json["fulfillment"]!.GetValue<string>());Assert.Equal("delivery",customer.Json["checkout"]!["method"]!.GetValue<string>());
            Assert.Contains(customer.Json["events"]!.AsArray(),e=>e!["status"]!.GetValue<string>()==stage);
        }
    }
}
