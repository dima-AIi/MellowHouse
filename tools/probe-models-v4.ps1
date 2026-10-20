$cfg=Get-Content src/MellowHouse.Web/App_Data/assistant-secrets.json -Raw | ConvertFrom-Json
$checks=foreach($model in @('nvidia/nemotron-3-ultra-550b-a55b','nvidia/nemotron-3.5-super-vl-120b-a12b','nvidia/nemotron-3.5-lightning-30b-a3b','openai/gpt-oss-20b')) {
 $watch=[Diagnostics.Stopwatch]::StartNew()
 try {$body=@{model=$model;messages=@(@{role='system';content='Отвечай кратко по-русски. Верни только JSON: {"text":"приветствие посетителю кафе","productIds":[],"suggestions":[]}.'},@{role='user';content='Привет'});max_tokens=500;temperature=0.2;stream=$false;chat_template_kwargs=@{enable_thinking=$false};reasoning_effort='low'} | ConvertTo-Json -Depth 6
 $res=Invoke-RestMethod https://integrate.api.nvidia.com/v1/chat/completions -Method Post -Headers @{Authorization=('Bearer '+$cfg.Assistant.ApiKey)} -ContentType 'application/json; charset=utf-8' -Body $body -TimeoutSec 18
 [pscustomobject]@{model=$model;seconds=[Math]::Round($watch.Elapsed.TotalSeconds,1);text=$res.choices[0].message.content;ok=[bool]$res.choices[0].message.content}
 } catch {[pscustomobject]@{model=$model;seconds=[Math]::Round($watch.Elapsed.TotalSeconds,1);error=$_.Exception.Message;ok=$false}}
}
$checks | ConvertTo-Json -Depth 4 | Set-Content artifacts/model-probe-v4-windows.json
$checks | ConvertTo-Json -Depth 4
