from pathlib import Path
import sqlite3,json
root=Path(__file__).resolve().parents[1]
db=sqlite3.connect(root/'src/MellowHouse.Web/App_Data/mellowhouse.db');db.row_factory=sqlite3.Row
categories=list(db.execute('select * from Categories order by SortOrder'))
products=list(db.execute('select * from Products order by SortOrder,Id'))
assert len(products)==78
text='# Каталог Mellow House — 78 позиций\n\nОбновлено 21.10.2026. Исходный план 73 товаров полностью покрыт; добавлены ещё 5 напитков. Старые 32 товара, фотографии и правки сохранены. Новые позиции добавляются один раз через catalog.complete.v6. Дальнейшее управление — в админке.\n\nМатча: классическая, клубничная и манговая. Новые коктейли/бабл-ти: Бабл-гам, Таро и Манго. У каждого товара описание, состав, аллергены, порция и доступные настройки; цена опций рассчитывается сервером. Неизвестные себестоимость и пищевая ценность не выдумываются.\n\n'
for category in categories:
    selected=[p for p in products if p['CategoryId']==category['Id']]
    text+=f'## {category["Name"]} — {len(selected)}\n\n| Товар | Порция | Базовая цена | Настройки |\n|---|---|---:|---|\n'
    for p in selected:
        groups=json.loads(p['GroupsJson']);labels=', '.join(g['label'] for g in groups) or 'Стандартная подача'
        text+=f'| {p["Name"]} | {p["Portion"]} | {p["Price"]} ₽ | {labels} |\n'
    text+='\n'
text+='Данные наполнения: Data/catalog.json (2), catalog-expanded.json (30), catalog-completion.json (46). Источник работающего сайта — БД. Происхождение изображений и точные промпты — asset-manifest.json, подача — PRODUCT-PHOTOGRAPHY.md.\n'
(root/'docs/CATALOG.md').write_text(text,encoding='utf-8')
print(json.dumps({'documentedProducts':len(products),'categories':len(categories)}))
