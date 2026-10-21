using System.Text.Json;
using System.Text.RegularExpressions;
using MellowHouse.Web.Data;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace MellowHouse.Web.Features;

public sealed record StoredMedia(byte[] Bytes,string ContentType);

public sealed class MediaService(IWebHostEnvironment env,ShopDbContext db)
{
    public static bool SafePath(string path,params string[] prefixes)=>!string.IsNullOrEmpty(path)&&path.Length<220&&!path.Contains("..")&&!path.Contains('\\')&&Regex.IsMatch(path,@"^/[a-zA-Z0-9_./-]+$")&&prefixes.Any(path.StartsWith);
    static bool StoredPath(string path)=>Regex.IsMatch(path??"",@"^/media/[a-f0-9]{32}\.(png|webp|jpg)$");
    static string Key(string path)=>"media.v8."+Path.GetFileName(path);
    public bool Exists(string path)=>StoredPath(path)?db.SiteSettings.Any(s=>s.Key==Key(path)):SafePath(path,"/images/","/uploads/")&&File.Exists(Path.Combine(env.WebRootPath,path.TrimStart('/')));
    public bool AssetExists(string path)=>Exists(path)||SafePath(path,"/videos/")&&File.Exists(Path.Combine(env.WebRootPath,path.TrimStart('/')));
    public async Task<StoredMedia?> GetAsync(string path,CancellationToken ct)
    {
        if(!StoredPath(path))return null;
        var row=await db.SiteSettings.FindAsync([Key(path)],ct);
        return row is null?null:JsonSerializer.Deserialize<StoredMedia>(row.Json,JsonDefaults.Options);
    }
    public async Task ValidateProductAsync(string path,CancellationToken ct)
    {
        if(!Exists(path))throw new ShopException("Добавьте фотографию товара с прозрачным фоном.");
        var extension=Path.GetExtension(path).ToLowerInvariant();
        if(extension is not(".png" or ".webp"))throw new ShopException("Для товара нужен прозрачный PNG или WebP.");
        var stored=await GetAsync(path,ct);
        var bytes=stored?.Bytes??await File.ReadAllBytesAsync(Path.Combine(env.WebRootPath,path.TrimStart('/')),ct);
        Decode(bytes,true);
    }
    public async Task<object> UploadAsync(IFormFile file,bool product,CancellationToken ct)
    {
        if(file.Length is <32 or >8388608)throw new ShopException("Выберите фотографию размером до 8 МБ.");
        using var stream=new MemoryStream();await file.CopyToAsync(stream,ct);var data=stream.ToArray();
        var png=data.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10});
        var webp=data.Length>12&&data.AsSpan(0,4).SequenceEqual("RIFF"u8)&&data.AsSpan(8,4).SequenceEqual("WEBP"u8);
        var jpeg=data.Length>3&&data[0]==255&&data[1]==216&&data[2]==255;
        if(product&&!png&&!webp)throw new ShopException("Для товара поддерживаются только PNG и WebP с прозрачным фоном. JPG, HEIC и SVG не подходят.");
        if(!png&&!webp&&!jpeg)throw new ShopException("Поддерживаются PNG, WebP и JPG.");
        var extension=png?".png":webp?".webp":".jpg";var supplied=Path.GetExtension(file.FileName).ToLowerInvariant();
        if(supplied!=extension&&!(jpeg&&supplied==".jpeg"))throw new ShopException("Расширение файла не соответствует изображению. Экспортируйте его как PNG или WebP.");
        var (width,height)=Decode(data,product);var name=Guid.NewGuid().ToString("N")+extension;
        // PostgreSQL keeps uploads across Render restarts; the local DB uses the same path.
        db.SiteSettings.Add(new(){Key=Key("/media/"+name),Json=JsonSerializer.Serialize(new StoredMedia(data,png?"image/png":webp?"image/webp":"image/jpeg"),JsonDefaults.Options)});
        await db.SaveChangesAsync(ct);
        return new{path="/media/"+name,width,height,transparent=product};
    }
    static (int,int) Decode(byte[] data,bool requireAlpha)
    {
        try
        {
            using var encoded=SKData.CreateCopy(data);using var codec=SKCodec.Create(encoded);
            if(codec is null)throw new ShopException("Файл повреждён или не является фотографией.");
            var info=codec.Info;
            if(info.Width is <32 or >4096||info.Height is <32 or >4096||(long)info.Width*info.Height>12000000)
                throw new ShopException("Размер фотографии: от 32 px, не более 4096 px по стороне и 12 млн пикселей.");
            if(codec.FrameCount>1)throw new ShopException("Выберите обычную фотографию без анимации.");
            using var image=new SKBitmap(new SKImageInfo(info.Width,info.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul));
            if(codec.GetPixels(image.Info,image.GetPixels())!=SKCodecResult.Success)throw new ShopException("Не удалось полностью прочитать фотографию.");
            var pixels=image.Bytes;var transparent=0;var visible=0;
            for(var i=3;i<pixels.Length;i+=4){if(pixels[i]==0)transparent++;if(pixels[i]>0)visible++;}
            if(requireAlpha&&(transparent<info.Width*info.Height/100||visible<info.Width*info.Height/100))
                throw new ShopException("Нужен видимый товар на настоящем прозрачном фоне. Белый фон или нарисованная шахматная сетка не подходят.");
            return(info.Width,info.Height);
        }
        catch(ShopException){throw;}
        catch{throw new ShopException("Файл повреждён или формат не поддерживается. Экспортируйте PNG или WebP и попробуйте снова.");}
    }
}
