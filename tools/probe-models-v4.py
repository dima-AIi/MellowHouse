import json,urllib.request,concurrent.futures,time,pathlib
r=pathlib.Path(r'D:\дима\MellowHouse');cfg=json.loads((r/'src/MellowHouse.Web/App_Data/assistant-secrets.json').read_text(encoding='utf-8'))['Assistant'];key=cfg['ApiKey']
models=['nvidia/nemotron-3-super-120b-a12b','nvidia/nemotron-3-nano-30b-a3b','openai/gpt-oss-20b']
def probe(model):
 start=time.monotonic()
 try:
  data=json.dumps({'model':model,'messages':[{'role':'system','content':'Отвечай кратко по-русски. Верни JSON {"text":"приветствие посетителю кафе","productIds":[],"suggestions":[]}.'},{'role':'user','content':'Привет'}],'max_tokens':180,'temperature':0.2,'stream':False,'chat_template_kwargs':{'enable_thinking':False},'reasoning_effort':'low'}).encode()
  req=urllib.request.Request('https://integrate.api.nvidia.com/v1/chat/completions',data,{'Authorization':'Bearer '+key,'Content-Type':'application/json'})
  with urllib.request.urlopen(req,timeout=22) as res:body=json.load(res)
  content=body['choices'][0]['message'].get('content') or '';return {'model':model,'ok':bool(content),'seconds':round(time.monotonic()-start,1),'text':content[:350]}
 except Exception as e:return {'model':model,'ok':False,'error':type(e).__name__,'status':getattr(e,'code',None),'seconds':round(time.monotonic()-start,1)}
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:result=list(pool.map(probe,models))
(r/'artifacts/model-probe-v4.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
