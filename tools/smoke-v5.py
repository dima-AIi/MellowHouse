from pathlib import Path
import urllib.request,json
r=Path(__file__).resolve().parents[1];base='http://127.0.0.1:5192'
routes=json.loads((r/'artifacts/release-v4-smoke.json').read_text())['routes']+[{'route':'/js/welcome-state.js'}]
for route in routes:
    with urllib.request.urlopen(base+route['route']) as response:
        route['status']=response.status;assert response.status==200
with urllib.request.urlopen(urllib.request.Request(base+'/videos/coffee-full-v5.mp4',headers={'Range':'bytes=0-1023'})) as response:video=response.status;assert video==206
with urllib.request.urlopen(base+'/api/catalog') as response:catalog=json.load(response)
private=[str(p.relative_to(r/'artifacts/publish-v5')) for p in (r/'artifacts/publish-v5').rglob('*') if p.is_file() and (p.suffix=='.db' or p.name in ['admin-access.json','assistant-secrets.json','sample-history-v3-archive.json'])];assert not private
publicLeaks=[]
for p in (r/'artifacts/publish-v5/wwwroot').rglob('*'):
    if p.is_file() and p.suffix in ['.js','.json','.css','.html','.svg'] and 'nvapi-' in p.read_text(encoding='utf-8'):publicLeaks.append(str(p))
assert not publicLeaks
result={'routes':routes,'videoStatus':video,'privateFiles':len(private),'publicKeyLeaks':len(publicLeaks),'categories':len(catalog['categories']),'products':len(catalog['products'])}
assert result['products']==32 and result['categories']==10
(r/'artifacts/release-v5-smoke.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
