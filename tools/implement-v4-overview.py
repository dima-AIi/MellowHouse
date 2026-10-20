from pathlib import Path
r=Path(r'D:\дима\MellowHouse\src\MellowHouse.Web');p=r/'wwwroot/js/admin.js';s=p.read_text(encoding='utf-8');s=s[:s.index('async function overview()')];s=s.replace('else await overview();',"else await(await import('./admin-overview.js?v=20261020-v4')).initOverview(host);");p.write_text(s,encoding='utf-8')
p=r/'Features/AdminService.cs';s=p.read_text(encoding='utf-8');idx=s.index('    private sealed class ReportRow');s=s[:idx]+'''    public async Task<byte[]> FinanceCsvAsync(string? from,string? to,CancellationToken ct)
    {
        var report=JsonSerializer.SerializeToElement(await ReportAsync(from,to,ct),JsonDefaults.Options);
        static string Safe(string value)=>"\\\""+(value.StartsWith('=')||value.StartsWith('+')||value.StartsWith('-')||value.StartsWith('@')?"'":"")+value.Replace("\\\"","\\\"\\\"")+"\\\"";
        var b=new StringBuilder("Показатель;Сумма, ₽\\r\\n");
        foreach(var item in new[]{("Поступления","collected"),("Себестоимость учтённая","knownCost"),("Расходы кафе","operatingExpenses"),("Результат по учтённым затратам","provisionalResult")})b.AppendLine(Safe(item.Item1)+";"+report.GetProperty(item.Item2).GetInt64());
        b.AppendLine("Полная себестоимость;"+(!report.GetProperty("missingCost").GetBoolean()?"Да":"Нет"));b.AppendLine("Дата;Категория;Сумма;Комментарий");
        foreach(var e in report.GetProperty("expenses").EnumerateArray())b.AppendLine(string.Join(';',Safe(e.GetProperty("date").GetString()!),Safe(e.GetProperty("category").GetString()!),e.GetProperty("amount").GetInt32(),Safe(e.GetProperty("note").GetString()!)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(b.ToString())).ToArray();
    }
'''+s[idx:];p.write_text(s,encoding='utf-8')
p=r/'Features/AdminApiEndpoints.cs';s=p.read_text(encoding='utf-8');s=s.replace('        g.MapGet("/report.csv",','        g.MapGet("/finance.csv",async(string? from,string? to,AdminService s,CancellationToken ct)=>Results.File(await s.FinanceCsvAsync(from,to,ct),"text/csv; charset=utf-8","mellow-house-finance.csv"));\n        g.MapGet("/report.csv",');p.write_text(s,encoding='utf-8')
# Apply one shared module version so all imports share the same state and no old auth module remains cached.
for p in (r/'wwwroot/js').glob('*.js'):
 s=p.read_text(encoding='utf-8').replace('20261020-v3','20261020-v4');p.write_text(s,encoding='utf-8')
p=r/'wwwroot/sw.js';s=p.read_text(encoding='utf-8').replace('mellowhouse-static-v3','mellowhouse-static-v4');p.write_text(s,encoding='utf-8')
print('Overview, expenses and cache version saved')
