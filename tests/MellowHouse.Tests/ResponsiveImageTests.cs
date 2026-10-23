using System.Net;
using System.Text;
using Xunit;

namespace MellowHouse.Tests;

public sealed class ResponsiveImageTests
{
    [Fact] public async Task ProductUsesExistingResponsiveVariants_AndOriginalAssetStillWorks()
    {
        using var f=new ShopFactory();using var b=new Buyer(f);await b.State();
        var source=(await b.Send("/api/products/vanilla-raf")).Json["image"]!.GetValue<string>();
        var html=Encoding.UTF8.GetString((await b.Raw("/Product/vanilla-raf")).Bytes);
        foreach(var width in new[]{320,480,640,768,960})
        {
            var variant="/images/optimized/"+source[8..]+"-"+width+".webp";
            Assert.Contains(variant,html);Assert.Equal(HttpStatusCode.OK,(await b.Raw(variant)).Status);
        }
        Assert.Contains("srcset=",html);Assert.Equal(HttpStatusCode.OK,(await b.Raw(source)).Status);
    }
}
