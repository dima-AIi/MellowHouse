using System.Collections.Concurrent;
namespace MellowHouse.Web.Features;
public sealed class AssistantHealth
{
    private readonly ConcurrentDictionary<string,DateTime> retryAfter=new();
    public string? LastModel {get;private set;}
    public bool Available(string model)=>!retryAfter.TryGetValue(model,out var until)||until<=DateTime.UtcNow;
    public void Failure(string model)=>retryAfter[model]=DateTime.UtcNow.AddSeconds(60);
    public void Success(string model){retryAfter.TryRemove(model,out _);LastModel=model;}
}
