using Microsoft.AspNetCore.Razor.TagHelpers;

namespace MellowHouse.Web.TagHelpers;

[HtmlTargetElement("img",Attributes="src")]
public sealed class ResponsiveProductImageTagHelper(IWebHostEnvironment environment):TagHelper
{
    public override void Process(TagHelperContext context,TagHelperOutput output)
    {
        var src=output.Attributes["src"]?.Value?.ToString();
        if(src is null||!(src.StartsWith("/images/products/",StringComparison.Ordinal)||src.StartsWith("/images/categories/",StringComparison.Ordinal))||src.Contains(".."))return;
        string Variant(int width)=>$"/images/optimized/{src[8..]}-{width}.webp";
        if(!environment.WebRootFileProvider.GetFileInfo(Variant(640).TrimStart('/')).Exists)return;
        var detail=int.TryParse(output.Attributes["width"]?.Value?.ToString(),out var width)&&width>=900;
        output.Attributes.SetAttribute("src",Variant(640));
        var widths = src.StartsWith("/images/categories/",StringComparison.Ordinal) ? new[]{320,480,640} : new[]{320,480,640,768,960};
        output.Attributes.SetAttribute("srcset",string.Join(", ",widths.Select(w=>$"{Variant(w)} {w}w")));
        output.Attributes.SetAttribute("sizes",detail?"(max-width: 700px) calc(100vw - 32px), (max-width: 1100px) 45vw, 560px":"(max-width: 700px) 46vw, (max-width: 1100px) 30vw, 320px");
    }
}
