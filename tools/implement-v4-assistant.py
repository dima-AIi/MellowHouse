from pathlib import Path
r=Path(r'D:\дима\MellowHouse\src\MellowHouse.Web')
p=r/'Data/SampleRevision.cs';s=p.read_text(encoding='utf-8').replace('db.ShoppingCarts','db.Carts').replace('Audit.Log(db,null,','Audit.Log(db,Guid.Empty,');p.write_text(s,encoding='utf-8')
p=r/'Features/NvidiaMenuAssistant.cs';s=p.read_text(encoding='utf-8');s=s.replace('ILogger<NvidiaMenuAssistant> logger)','ILogger<NvidiaMenuAssistant> logger, AssistantHealth? health=null)');start=s.index('        try\n        {\n            using var request');s=s[:start]+'''        health??=new AssistantHealth();
        using var total=CancellationTokenSource.CreateLinkedTokenSource(ct);total.CancelAfter(TimeSpan.FromSeconds(35));
        foreach(var model in Models(config))
        {
        if(!health.Available(model)||total.IsCancellationRequested)continue;
        using var attempt=CancellationTokenSource.CreateLinkedTokenSource(total.Token);attempt.CancelAfter(TimeSpan.FromSeconds(12));
'''+s[start:];s=s.replace('model=config["Assistant:Model"]??"openai/gpt-oss-20b",','model,');s=s.replace('.SendAsync(request, ct)','.SendAsync(request, attempt.Token)').replace('ReadAsStreamAsync(ct), cancellationToken:ct','ReadAsStreamAsync(attempt.Token), cancellationToken:attempt.Token');s=s.replace('            return new(text, ids, Suggestions);','            health.Success(model);return new(text, ids, Suggestions);');s=s.replace('            var answer=await fallback.ReplyAsync(message, ct,recent);\n            return answer with {Text="Онлайн-консультант сейчас недоступен. Помогу по нашему меню.\\n\\n"+answer.Text};','''            health.Failure(model);
            if(e is HttpRequestException {StatusCode:System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden})break;
''');end=s.rfind('    }\n}');s=s[:end]+'''        }
        var answer=await fallback.ReplyAsync(message, ct,recent);
        return answer with {Text="Онлайн-консультант сейчас недоступен. Помогу по нашему меню.\\n\\n"+answer.Text};
'''+s[end:];idx=s.index('    public static string[] Recent');s=s[:idx]+'''    public static string[] Models(IConfiguration config)
    {
        var list=config.GetSection("Assistant:Models").Get<string[]>();
        return (list is {Length:>0}?list:config["Assistant:Model"] is {} model?[model]:new[]{"nvidia/nemotron-3-ultra-550b-a55b","nvidia/nemotron-3.5-super-vl-120b-a12b","nvidia/nemotron-3.5-lightning-30b-a3b"}).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct().Take(4).ToArray();
    }
'''+s[idx:];p.write_text(s,encoding='utf-8')
p=r/'Program.cs';s=p.read_text(encoding='utf-8').replace('builder.Services.AddScoped<LocalMenuAssistant>();','builder.Services.AddSingleton<AssistantHealth>();\nbuilder.Services.AddScoped<LocalMenuAssistant>();');p.write_text(s,encoding='utf-8')
p=r/'Features/AdminApiEndpoints.cs';s=p.read_text(encoding='utf-8').replace('SiteService s,IConfiguration config,CancellationToken ct)=>Results.Ok(new{site=', 'SiteService s,IConfiguration config,AssistantHealth health,CancellationToken ct)=>Results.Ok(new{site=');s=s.replace('assistantModel=config["Assistant:Model"]','assistantModel=NvidiaMenuAssistant.Models(config).FirstOrDefault(),assistantModels=NvidiaMenuAssistant.Models(config),assistantLastModel=health.LastModel');p.write_text(s,encoding='utf-8')
# Runtime-only key file: change model order without displaying or copying the key.
p=r/'App_Data/assistant-secrets.json';import json;c=json.loads(p.read_text(encoding='utf-8'));models=['nvidia/nemotron-3-ultra-550b-a55b','nvidia/nemotron-3.5-super-vl-120b-a12b','nvidia/nemotron-3.5-lightning-30b-a3b'];c['Assistant']['Model']=models[0];c['Assistant']['Models']=models;p.write_text(json.dumps(c,ensure_ascii=False,indent=2),encoding='utf-8')
print('NVIDIA model chain saved; key not printed')
