"""Prepare the remaining agreed menu, preserving existing product identities."""
import json,sqlite3,copy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
full=json.loads((ROOT/'content-staging/catalog-full.json').read_text(encoding='utf-8'))['products']
db=sqlite3.connect(ROOT/'src/MellowHouse.Web/App_Data/mellowhouse.db')
existing={r[0] for r in db.execute('select Id from Products')}
items=[copy.deepcopy(p) for p in full if p['id'] not in existing]

recipes={
'potato-waffles':'Картофель, пшеничная мука, яйцо, молоко, сливочное масло, бекон, яйцо пашот, сметана, укроп, соль.',
'berry-granola':'Овсяные хлопья, миндаль, грецкий орех, мёд, натуральный йогурт, клубника, черника, малина.',
'flat-white':'Двойной эспрессо, коровье молоко. Сахар добавляется только по выбору.',
'cold-brew':'Кофе холодного заваривания, питьевая вода, лёд.',
'espresso-tonic':'Эспрессо, тоник с сахаром, лёд, апельсин.',
'jasmine-tea':'Зелёный чай с жасмином, питьевая вода.',
'ginger-lemon':'Имбирь, лимон, мёд, питьевая вода.',
'cocoa':'Какао-порошок, коровье молоко, тёмный шоколад с соевым лецитином, сахар.',
'berry-iced-tea':'Чёрный чай, малина, чёрная смородина, сахарный сироп, питьевая вода, лёд.',
'lemon-mint':'Лимонный сок, лимон, мята, сахарный сироп, газированная вода, лёд.',
'mango-passion':'Пюре манго, пюре маракуйи, сахарный сироп, газированная вода, лёд.',
'peach-iced-tea':'Чёрный чай, персиковое пюре, лимон, сахарный сироп, питьевая вода, лёд.',
'berry-smoothie':'Малина, черника, банан, яблочный сок без добавленного сахара.',
'tropical-smoothie':'Манго, ананас, банан, апельсиновый сок без добавленного сахара.',
'chocolate-shake':'Коровье молоко, шоколадное мороженое (молоко, сливки, сахар, какао), шоколадный соус с соевым лецитином, взбитые сливки.',
'caramel-shake':'Коровье молоко, ванильное мороженое (молоко, сливки, сахар, ваниль), карамельный соус (сахар, сливки, соль), взбитые сливки.',
'mojito':'Лайм, мята, тростниковый сироп, газированная вода, лёд. Без алкоголя.',
'pina-colada':'Ананасовый сок, кокосовые сливки, ананас, лёд. Без алкоголя.',
'roastbeef-mustard':'Пшеничный хлеб на закваске, ростбиф, горчичный соус, рукола, красный лук, уксус, сахар, соль.',
'caprese-panini':'Пшеничный хлеб панини, моцарелла, томаты, базилик, песто (базилик, оливковое масло, пармезан, кедровый орех, чеснок), соль.',
'chicken-wrap':'Пшеничная тортилья, куриное филе, томаты, огурец, листья салата, натуральный йогурт, чеснок, лимонный сок, соль.',
'cheese-sticks':'Моцарелла, пшеничная мука, яйцо, панировочные сухари, растительное масло, томатный соус.',
'potato-wedges':'Картофель, растительное масло, паприка, соль, чесночный соус (сметана, чеснок, укроп).',
'warm-beef-salad':'Говядина, листья салата, томаты черри, сладкий перец, цукини, бальзамический уксус, оливковое масло, соль.',
'grilled-chicken-salad':'Куриное филе, листья салата, томаты, огурец, красный лук, грецкий орех, оливковое масло, лимонный сок, соль.',
'chicken-bowl':'Куриное филе, рис, авокадо, кукуруза, огурец, томаты, листья салата, лимонный сок, оливковое масло, соль.',
'vegetable-bowl':'Киноа, авокадо, цукини, морковь, сладкий перец, томаты, нут, кунжутная паста тахини, лимонный сок, оливковое масло, соль.',
'shrimp-avocado':'Креветки, авокадо, томаты черри, листья салата, апельсиновый сок, лимонный сок, оливковое масло, соль.',
'chicken-steak':'Куриное филе, картофель, цукини, сладкий перец, морковь, оливковое масло, петрушка, лимонный сок, соль.',
'pork-steak':'Свинина, картофель, сладкий перец, цукини, сливки, чёрный перец, сливочное масло, соль.',
'bbq-ribs':'Свиные рёбрышки, соус BBQ (томаты, сахар, уксус, специи), картофель, белокочанная капуста, морковь, майонез с яйцом и горчицей, соль.',
'stuffed-potato':'Картофель, куриное филе, шампиньоны, сыр, сливочное масло, укроп, соль.',
'shrimp-pasta':'Лингвини из пшеницы твёрдых сортов без яйца, креветки, томаты, чеснок, базилик, оливковое масло, соль.',
'chicken-rice':'Рис, куриное филе, брокколи, сладкий перец, соус терияки (соевый соус с пшеницей, сахар, имбирь), растительное масло.',
'four-cheese':'Пшеничное тесто, моцарелла, чеддер, пармезан, голубой сыр, сливочный соус, оливковое масло.',
'chicken-mushroom-pizza':'Пшеничное тесто, куриное филе, шампиньоны, моцарелла, сливочный соус, оливковое масло.',
'apple-crumble':'Яблоко, пшеничная мука, сливочное масло, сахар, корица, ванильное мороженое (молоко, сливки, сахар, ваниль).',
'honey-cake':'Пшеничная мука, мёд, яйцо, сахар, сливочное масло, сметана, сливки.',
'berry-tart':'Пшеничная мука, сливочное масло, яйцо, сахар, молоко, кукурузный крахмал, ваниль, клубника, малина, черника.',
'chocolate-cookie':'Пшеничная мука, сливочное масло, яйцо, сахар, тёмный шоколад с соевым лецитином, сода, соль.',
'tiramisu':'Печенье савоярди (пшеничная мука, яйцо, сахар), маскарпоне, пастеризованное яйцо, сливки, эспрессо, какао. Без алкоголя.'}

def option(id,label,price=0,**kw):return dict(id=id,label=label,price=price,**kw)
def group(id,label,options,default=None,multiple=False):return dict(id=id,label=label,options=options,defaults=default if default is not None else ([] if multiple else [options[0]['id']]),multiple=multiple,required=not multiple)
def sizes(base,other,delta=40):return group('size','Объём',[option(str(base),f'{base} мл'),option(str(other),f'{other} мл',delta)])
milk=group('milk','Молоко',[option('regular','Обычное'),option('oat','Овсяное',60,removeAllergens=['молоко'],addAllergens=['овёс'])])
extra_syrup=group('syrup','Сироп',[option('standard','По рецепту'),option('none','Без дополнительного сиропа')])
sugar=group('sweet','Сахар',[option('none','Не добавлять'),option('standard','Добавить сахар')])
no_honey=group('honey','Мёд',[option('standard','С мёдом'),option('none','Без мёда')])
for index,p in enumerate(items):
    p['ingredients']=recipes[p['id']];p['image']=f"/images/products/{p['id']}-v6.png";p['sortOrder']=index+1
    # The old staging list was a sketch: avoid promising sugar-free ice cream or syrup-based tonics.
    if p['id']=='flat-white':p['groups']=[sizes(200,250),copy.deepcopy(milk),copy.deepcopy(sugar),p['groups'][-1]]
    elif p['id'] in ['cold-brew','espresso-tonic']:p['groups']=[sizes(300,400) if p['id']=='cold-brew' else sizes(350,450)]
    elif p['id']=='jasmine-tea':p['groups']=[sizes(400,600,80)]
    elif p['id']=='ginger-lemon':p['groups']=[sizes(400,600,80),copy.deepcopy(no_honey)]
    elif p['id']=='cocoa':p['groups']=[sizes(300,400),copy.deepcopy(milk),group('sweet','Добавленный сахар',[option('standard','По рецепту'),option('none','Без добавленного сахара')])];p['allergens']=['молоко','соя']
    elif p['id'] in ['berry-iced-tea','lemon-mint','mango-passion','peach-iced-tea','mojito']:p['groups']=[sizes(400,500),copy.deepcopy(extra_syrup)]
    elif p['id'] in ['berry-smoothie','tropical-smoothie']:p['groups']=[sizes(300,400,70)]
    elif p['id'] in ['chocolate-shake','caramel-shake']:
        p['groups']=[sizes(400,500,70),group('topping','Топпинг',[option('standard','С соусом и сливками'),option('none','Без соуса и сливок')])]
        if p['id']=='chocolate-shake':p['allergens']=['молоко','соя']
    elif p['id']=='pina-colada':p['groups']=[sizes(400,500,70)]
    if p['id']=='bbq-ribs':p['allergens']=['яйцо','горчица']
    if p['id']=='chicken-rice':p['allergens']=['соя','глютен']
    if p['id']=='chocolate-cookie':p['allergens']=['глютен','яйцо','молоко','соя']

def add(id,cat,name,price,desc,ingredients,photo,allergens,groups,popular=False):
    items.append(dict(id=id,categoryId=cat,name=name,price=price,portion='400 мл',description=desc,ingredients=ingredients,image=f'/images/products/{id}-v6.png',photo=photo,groups=groups,allergens=allergens,popular=popular,available=True,sortOrder=len(items)+1))
fruit_matcha_groups=[sizes(400,500,70),copy.deepcopy(milk)]
add('strawberry-matcha','cold','Клубничная матча-латте',390,'Яркая клубника, нежное молоко и зелёная матча. Подаём со льдом.','Зелёный чай матча, клубничное пюре с сахаром, коровье молоко, питьевая вода, лёд.','iced strawberry matcha latte in one plain tall clear glass: a distinct red strawberry puree layer at the bottom, white milk and ice in the middle, vibrant emerald matcha at the top. No whipped cream and no garnish.',['молоко'],copy.deepcopy(fruit_matcha_groups),True)
add('mango-matcha','cold','Манговая матча-латте',390,'Сочное манго, холодное молоко и насыщенная матча в одном стакане.','Зелёный чай матча, пюре манго, коровье молоко, питьевая вода, лёд.','iced mango matcha latte in one tall clear glass: golden mango puree bottom, white milk and ice middle, emerald matcha top. No whipped cream and no garnish.',['молоко'],copy.deepcopy(fruit_matcha_groups))
add('bubblegum-shake','shakes','Коктейль Бабл-гам',370,'Нежный розовый молочный коктейль со вкусом жевательной резинки и сливочной шапкой.','Коровье молоко, ванильное мороженое (молоко, сливки, сахар, ваниль), сироп бабл-гам (сахар, вода, ароматизатор, пищевой краситель), взбитые сливки.','one tall clear glass of pastel pink bubblegum milkshake with a small neat white whipped cream swirl. Absolutely no chewing gum balls, no candy, no sprinkles and no fruit garnish.',['молоко'],[sizes(400,500,70),group('cream','Сливки',[option('yes','Со сливками'),option('no','Без сливок')])],True)
add('taro-bubble-tea','cold','Бабл-ти Таро',390,'Сливочный напиток с нежным таро, чёрным чаем и мягкими жемчужинами тапиоки.','Чёрный чай, пюре таро, коровье молоко, сахарный сироп, варёная тапиока (крахмал маниоки, сахар), питьевая вода, лёд.','one tall plain clear straight glass of softly lavender taro milk bubble tea with black cooked tapioca pearls at the base and a few ice cubes at the top; no cream, no garnish, no lid, no straw.',['молоко'],[sizes(400,500,70),copy.deepcopy(milk),copy.deepcopy(extra_syrup)])
add('mango-bubble-tea','cold','Бабл-ти Манго',370,'Холодный жасминовый чай с манго и мягкими жемчужинами тапиоки.','Жасминовый зелёный чай, пюре манго, сахарный сироп, варёная тапиока (крахмал маниоки, сахар), питьевая вода, лёд.','one tall plain clear glass of golden mango jasmine fruit bubble tea with black cooked tapioca pearls visible at the bottom and a few ice cubes; translucent bright mango tea, absolutely no milk, no cream, no fruit garnish, no lid, no straw.',[],[sizes(400,500,70),copy.deepcopy(extra_syrup)])

assert len(items)==46 and len({p['id'] for p in items})==46
destination=ROOT/'src/MellowHouse.Web/Data/catalog-completion.json';destination.write_text(json.dumps(items,ensure_ascii=False,indent=2),encoding='utf-8')
plan=[]
for p in items:
    prompt=("Use case: product-mockup. Asset type: one photorealistic premium cafe menu product cutout for Mellow House. Subject: "+p['photo']+". Show ONLY this complete product, with its whole neutral off-white ceramic plate or bowl when it is food, or its complete plain unbranded glass/cup when it is a drink. Preserve the specified food and toppings exactly. No table, no cafe background, no loose ingredients outside the vessel, no props, no utensils, no labels, no text, no logos. Genuine transparent alpha background, no opaque backdrop and no drawn checkerboard. Soft warm studio lighting from upper left, three-quarter front view, clean quiet premium bistro styling. Square composition, complete product centered with generous transparent padding all around. Realistic appetizing food photography, not illustration or a poster.")
    plan.append({'id':p['id'],'name':p['name'],'destination':str(ROOT/'src/MellowHouse.Web/wwwroot'/p['image'].lstrip('/')),'prompt':prompt})
(ROOT/'artifacts/menu-completion-image-plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'plannedOriginal':len(full),'existing':len(existing),'remainingOriginal':41,'newDrinks':5,'added':len(items),'final':len(existing)+len(items)},ensure_ascii=False))
