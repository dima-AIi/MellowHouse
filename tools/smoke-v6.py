"""Check the local Production publish and every menu page/image without modifying user data."""
from pathlib import Path
import urllib.request,json
root=Path(__file__).resolve().parents[1];base='http://127.0.0.1:5192'
routes=json.loads((root/'artifacts/release-v4-smoke.json').read_text())['routes']+[{'route':'/js/welcome-state.js'}]
for route in routes:
    with urllib.request.urlopen(base+route['route']) as response:
        route['status']=response.status;assert response.status==200
with urllib.request.urlopen(urllib.request.Request(base+'/videos/coffee-full-v5.mp4',headers={'Range':'bytes=0-1023'})) as response:
    video=response.status;assert video==206
with urllib.request.urlopen(base+'/api/catalog') as response:catalog=json.load(response)
assert len(catalog['products'])==78 and len(catalog['categories'])==10
checked=[]
for p in catalog['products']:
    with urllib.request.urlopen(base+'/Product/'+p['id']) as response:assert response.status==200
    with urllib.request.urlopen(urllib.request.Request(base+p['image'],method='HEAD')) as response:
        assert response.status==200 and response.headers['Content-Type'].startswith('image/') and int(response.headers['Content-Length'])>1000
    checked.append(p['id'])
publish=root/'artifacts/publish-v6'
private=[str(p.relative_to(publish)) for p in publish.rglob('*') if p.is_file() and (p.suffix=='.db' or p.name in ['admin-access.json','assistant-secrets.json','sample-history-v3-archive.json'])];assert not private
leaks=[str(p) for p in (publish/'wwwroot').rglob('*') if p.is_file() and p.suffix in ['.js','.json','.css','.html','.svg'] and 'nvapi-' in p.read_text(encoding='utf-8')];assert not leaks
result={'routes':routes,'videoStatus':video,'privateFiles':len(private),'publicKeyLeaks':len(leaks),'categories':len(catalog['categories']),'products':len(catalog['products']),'allProductPagesAndImages':len(checked)}
(root/'artifacts/release-v6-smoke.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
