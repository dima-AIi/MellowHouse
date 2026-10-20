"""Read-only data/asset checks plus provenance documents for the menu completion."""
from pathlib import Path
from PIL import Image
import json,hashlib,sqlite3,sys,html

ROOT=Path(__file__).resolve().parents[1]
WEB=ROOT/'src/MellowHouse.Web'
def load(path):return json.loads(path.read_text(encoding='utf-8-sig'))
items=load(WEB/'Data/catalog-completion.json')

def assets():
    preserved={x['id']:x['sha256'] for x in load(ROOT/'artifacts/menu-images-v6-preserved.json')}
    records=[];hashes=set();manifest=load(ROOT/'docs/asset-manifest.json')
    new_paths={p['image'].removeprefix('/') for p in items}
    manifest=[x for x in manifest if x['file'] not in new_paths]
    for p in items:
        path=WEB/'wwwroot'/p['image'].lstrip('/');meta=load(ROOT/'artifacts/menu-generated-v6'/f"{p['id']}.json")
        data=path.read_bytes();digest=hashlib.sha256(data).hexdigest();assert digest not in hashes,p['id'];hashes.add(digest)
        if p['id'] in preserved:assert preserved[p['id']]==digest,p['id']+' was unexpectedly changed'
        with Image.open(path) as im:
            assert im.mode=='RGBA',p['id'];alpha=im.getchannel('A');ext=alpha.getextrema();transparent=alpha.histogram()[0]/(im.width*im.height)
            assert ext[0]==0 and ext[1]>=250 and .08<transparent<.95,(p['id'],ext,transparent)
            assert max(im.size)<=4096 and im.width*im.height<=12000000 and len(data)<=8*1024*1024,p['id']
            records.append({'id':p['id'],'width':im.width,'height':im.height,'bytes':len(data),'alpha':True,'transparentFraction':round(transparent,3),'sha256':digest,'preserved':p['id'] in preserved})
        assert Path(meta['source']).exists(),p['id'];assert hashlib.sha256(Path(meta['source']).read_bytes()).hexdigest()==digest
        manifest.append({'file':p['image'].lstrip('/'),'product':p['name'],'source':'Собственная генерация built-in image_gen','conditions':'Создано для проекта; не является съёмкой реального кафе. Подтверждённый RGBA PNG.','prompt':meta['prompt'],'original':meta['source'],'reference':meta.get('reference'),'alpha':True,'style':meta.get('style','Natural cafe photography after user feedback')})
    assert len(records)==46
    (ROOT/'docs/asset-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'artifacts/menu-assets-v6-results.json').write_text(json.dumps(records,indent=2))
    cards=''.join(f'<article><img src="http://localhost:5186{html.escape(p["image"])}" alt=""><strong>{html.escape(p["name"])}</strong><small>{"Сохранено" if p["id"] in preserved else "Новый естественный стиль"}</small></article>' for p in items)
    page='<!doctype html><html lang="ru"><meta charset="utf-8"><title>Проверка фото меню</title><style>body{margin:20px;background:#f3f7f4;color:#183e31;font:14px Arial}main{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:15px}article{background:white;padding:10px;display:flex;flex-direction:column;gap:6px;min-width:0}img{width:100%;height:170px;object-fit:contain}small{color:#64756b}h1{font-size:22px}</style><h1>Проверка 46 новых фото: 37 сохранены, 9 по фотографическим референсам</h1><main>'+cards+'</main></html>'
    (ROOT/'artifacts/menu-gallery-v6.html').write_text(page,encoding='utf-8')
    source_doc=ROOT/'docs/ASSET-SOURCES.md';text=source_doc.read_text(encoding='utf-8');marker='\n## Завершение меню — 21.10.2026'
    if marker in text:text=text.split(marker)[0]
    text+=marker+'\n\n46 новых собственных RGBA PNG через встроенный image_gen. По последовательным уточнениям пользователя 37 готовых новых изображений оставлены побайтно прежними; последние 9 используют исходные иллюстрации как референсы фактуры, света и формы посуды, со спокойными цветами и простой подачей. Это сгенерированные изображения, не фотографии реальной кухни. Старые 32 фотографии не заменялись. Правила — PRODUCT-PHOTOGRAPHY.md, точные промпты/исходники — asset-manifest.json и artifacts/menu-generated-v6/.\n\n| Товар | Файл | Стиль |\n|---|---|---|\n'
    text+=''.join(f'| {p["name"]} | `{p["image"]}` | {"Сохранён готовый" if p["id"] in preserved else "Естественная подача"} |\n' for p in items)
    source_doc.write_text(text,encoding='utf-8')
    return {'newImages':len(records),'preservedImages':len(preserved),'naturalImages':len(records)-len(preserved),'maxBytes':max(x['bytes'] for x in records)}

def database():
    snapshot=Path((ROOT/'artifacts/menu-completion-update-backup.txt').read_text(encoding='utf-8').strip())
    old=sqlite3.connect(snapshot/'mellowhouse.db');new=sqlite3.connect(WEB/'App_Data/mellowhouse.db')
    result={'backup':str(snapshot),'integrity':new.execute('pragma integrity_check').fetchone()[0],'tables':{}}
    for table in ['Orders','Customers','PaymentAttempts','CartLines','Favorites','Categories','DataProtectionKeys','OrderEvents','OrderRefunds']:
        columns=[r[1] for r in old.execute('pragma table_info('+table+')')]
        if table=='Customers':columns.remove('LastActiveUtc')
        names=','.join('"'+x+'"' for x in columns);before=old.execute('select '+names+' from '+table).fetchall();after=new.execute('select '+names+' from '+table).fetchall()
        assert sorted(map(repr,before))==sorted(map(repr,after)),table
        result['tables'][table]={'before':len(before),'after':len(after),'unchanged':True}
    products=old.execute('select * from Products').fetchall();ids=[r[0] for r in products];after=new.execute('select * from Products where Id in ('+','.join('?' for _ in ids)+')',ids).fetchall();assert sorted(map(repr,products))==sorted(map(repr,after))
    image_hashes=load(snapshot/'images.json')
    for id,path in new.execute('select Id,Image from Products'):
        if id in image_hashes:assert hashlib.sha256((WEB/'wwwroot'/path.lstrip('/')).read_bytes()).hexdigest()==image_hashes[id],id+' old image changed'
    old_settings=dict(old.execute('select Key,Json from SiteSettings'));settings=dict(new.execute('select Key,Json from SiteSettings'));assert all(settings[k]==v for k,v in old_settings.items())
    count=new.execute('select count(*) from Products').fetchone()[0];result.update(oldProductsPreserved=len(products),oldImagesPreserved=len(image_hashes),products=count,addedProducts=count-len(products),oldSettingsPreserved=True)
    assert result['addedProducts']==46 and result['integrity']=='ok'
    (ROOT/'artifacts/menu-preservation-v6.json').write_text(json.dumps(result,indent=2));return result

if __name__=='__main__':print(json.dumps(database() if '--database' in sys.argv else assets()))
