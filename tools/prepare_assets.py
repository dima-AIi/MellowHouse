from pathlib import Path
import json
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[1]
source=root/'artifacts/assets-original'
web=root/'src/MellowHouse.Web/wwwroot'
assets=[]
def optimize(name,dest,width,alpha=False,item=None):
    path=source/(name+'.png')
    im=Image.open(path)
    if alpha:
        assert im.mode=='RGBA', f'{name}: отсутствует alpha'
        lo,hi=im.getchannel('A').getextrema()
        assert lo==0 and hi==255, f'{name}: прозрачность не настоящая'
    else: im=im.convert('RGB')
    im.thumbnail((width,width),Image.Resampling.LANCZOS)
    output=web/dest;output.parent.mkdir(parents=True,exist_ok=True)
    im.save(output,'WEBP',quality=88,method=6)
    meta={}
    if (source/(name+'.json')).exists(): meta=json.loads((source/(name+'.json')).read_text(encoding='utf-8-sig'))
    assets.append(dict(file=dest,product=item or name,source='Собственная генерация built-in image_gen',conditions='Сгенерировано для проекта; чужие фотографии не публикуются',prompt=meta.get('prompt','Собственная фотография кафе по согласованному заданию'),original=str(path),alpha=alpha))
    print(dest, output.stat().st_size)

optimize('vanilla-raf-cutout','images/products/vanilla-raf.webp',960,True,'Ванильный раф')
optimize('bbq-chicken-cutout','images/products/bbq-chicken.webp',960,True,'Сэндвич BBQ с курицей на тарелке')
optimize('hero-v2','images/hero-v2.webp',1920,item='Главная: кофе, сэндвич, салат, горячее')
optimize('hero-v2','images/hero-mobile-v2.webp',960,item='Главная, мобильная версия')
optimize('barista','images/barista.webp',1000,item='Бариста за приготовлением кофе')
optimize('menu-banner','images/menu-banner.webp',1440,item='Капучино и карамельный чизкейк, рекламный блок')
for name in ['egg-croissant','roastbeef-mustard','salmon-bowl','beef-medallions','cappuccino','cherry-lemonade','strawberry-shake','chocolate-fondant']:
    optimize(name,f'images/categories/{name}.webp',700,item='Фотокатегория: '+name)

for size in [192,512]:
    im=Image.new('RGB',(size,size),'#123f32'); d=ImageDraw.Draw(im)
    points=[(.22,.72),(.22,.28),(.32,.28),(.5,.56),(.68,.28),(.78,.28),(.78,.72),(.66,.72),(.66,.48),(.5,.72),(.34,.48),(.34,.72)]
    d.polygon([(int(x*size),int(y*size)) for x,y in points], fill='white')
    im.save(web/f'images/pwa-{size}.png')
    assets.append(dict(file=f'images/pwa-{size}.png',product='Иконка Mellow House',source='Собственная геометрическая эмблема M',conditions='Создана кодом в проекте',alpha=False))

(root/'docs/asset-manifest.json').write_text(json.dumps(assets,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# Источники изображений','', 'Все активные фотографии созданы встроенным image_gen для Mellow House. PNG исходники сохранены в `artifacts/assets-original/`; промпты — в соседних JSON и `docs/asset-manifest.json`. WebP — только оптимизация размера, без изменения содержания. У двух товарных изображений проверен настоящий альфа-канал.','', '| Файл | Назначение | Источник / условия |','|---|---|---|']
for a in assets: lines.append(f"| `{a['file']}` | {a['product']} | {a['source']}; {a['conditions']} |")
lines+=['','## Референсные сайты','', '- Joe: https://www.joejuice.com/policy/terms-of-use — фотографии защищены, публичный перенос без явного разрешения не допускается.','- Starbucks: https://www.starbucks.com/terms/starbucks-terms-of-use и https://about.starbucks.com/multimedia/2022/sbx20220815-coffeecommitment-cafepractices/ — медиа не используются для коммерческого каталога.','- Pret: https://www.pret.co.uk/en-GB/legal — права принадлежат Pret, разрешение для нашего меню не получено.','- Costa: https://www.costa.co.uk/menu — разрешение на использование фотографий для Mellow House не подтверждено.','- Dolce Vita: https://dolcevitacafe.ru/ — отдельное разрешение на фотографии не предоставлено.','- Из этих сайтов и из макетов фотографии в наш сайт не скачивались. Использовано только изучение композиции и поведения.','','## Шрифты','', 'Oswald и Golos Text: официальный https://github.com/google/fonts, SIL Open Font License 1.1. TTF и полные OFL-лицензии находятся в `wwwroot/fonts/`. SVG-иконки написаны в проекте.','','## Отложенное наполнение','', 'Предварительные фото полного ассортимента сохранены отдельно и не подключены как товары. Их соответствие рецептурам и пригодность нужно проверить на финальном этапе.']
(root/'docs/ASSET-SOURCES.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')

checkout=root/'src/MellowHouse.Web/Pages/Checkout.cshtml'
checkout.write_text(checkout.read_text(encoding='utf-8-sig').replace('</option value="demo-house">','</option><option value="demo-house">'),encoding='utf-8')
home=root/'src/MellowHouse.Web/Pages/Index.cshtml'
home.write_text(home.read_text(encoding='utf-8-sig').replace('/images/products/@(c.Item3).webp','/images/categories/@(c.Item3).webp'),encoding='utf-8')
for rel in ['Pages/Shared/_AppBanner.cshtml','Pages/App.cshtml']:
    p=root/'src/MellowHouse.Web'/rel
    p.write_text(p.read_text(encoding='utf-8-sig').replace('/images/products/salmon-bowl.webp','/images/products/bbq-chicken.webp').replace('Боул с лососем в мобильном меню','Сэндвич BBQ в мобильном меню'),encoding='utf-8')
