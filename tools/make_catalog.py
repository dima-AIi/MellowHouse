import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATS = [('breakfast','Завтраки'),('coffee','Кофе'),('tea','Чай и горячие напитки'),('cold','Холодные напитки'),('shakes','Коктейли'),('sandwiches','Сэндвичи и закуски'),('salads','Салаты и боулы'),('hot','Горячие блюда'),('pizza','Пицца'),('desserts','Десерты и выпечка')]
products = []
def choice(id,label,price=0,**kw): return dict(id=id,label=label,price=price,**kw)
def group(id,label,options,default=None,multiple=False):
    return dict(id=id,label=label,options=options,defaults=default or ([] if multiple else [options[0]['id']]),multiple=multiple,required=not multiple)
milk = group('milk','Молоко',[choice('regular','Обычное'),choice('oat','Овсяное',60,removeAllergens=['молоко'],addAllergens=['овёс'])])
sweet = group('sweet','Сладость',[choice('standard','Стандартная'),choice('none','Без сахара')])
shot = group('extras','Дополнительно',[choice('espresso','Дополнительный шот эспрессо',50)],multiple=True)
def sizes(label,rows,default): return group('size',label,[choice(*r) for r in rows],[default])
coffee_size = sizes('Объём',[('250','250 мл',-40),('300','300 мл',0),('400','400 мл',40)],'300')
sand_size = sizes('Размер',[('standard','Стандарт',0),('large','Большой',130)],'standard')
cheese = group('extras','Дополнительно',[choice('cheese','Добавить сыр',60,addAllergens=['молоко'])],multiple=True)
def exclude(*names): return group('exclude','Исключить ингредиенты',[choice(str(i),'Без '+n) for i,n in enumerate(names)],multiple=True)
sauce = group('sauce','Соус',[choice('standard','Стандартно'),choice('apart','Отдельно')])
def add(cat,id,name,price,portion,desc,photo,groups=None,allergens=None,popular=False):
    products.append(dict(id=id,categoryId=cat,name=name,price=price,portion=portion,description=desc,ingredients=desc,image=f'/images/products/{id}.webp',photo=photo,groups=groups or [],allergens=allergens or [],popular=popular,available=True))

add('breakfast','bacon-omelette','Омлет с беконом',360,'320 г','Нежный омлет, хрустящий бекон, томаты и зелень.','fluffy folded golden omelette with crispy bacon strips, cherry tomatoes and mixed greens on a white ceramic plate',[exclude('томатов'),cheese],['яйцо','молоко'])
add('breakfast','egg-croissant','Круассан с яйцом',350,'200 г','Горячий круассан, скрембл, салат и сливочный сыр.','golden flaky croissant sandwich filled with scrambled egg, soft cream cheese and fresh green lettuce',[exclude('салата')],['глютен','яйцо','молоко'])
add('breakfast','potato-waffles','Картофельные вафли',390,'280 г','Хрустящие картофельные вафли, бекон, яйцо пашот и зелень.','two crispy golden potato waffles topped with poached egg and bacon, fresh herbs and a little sour cream',[exclude('бекона')],['глютен','яйцо','молоко'])
add('breakfast','berry-granola','Гранола с ягодами',280,'250 г','Домашняя гранола, натуральный йогурт, свежие ягоды и мёд.','white bowl of thick yogurt with golden oat granola, blueberries raspberries and honey',[],['молоко','овёс','орехи'])
add('breakfast','syrniki','Сырники',320,'260 г','Творожные сырники со сметаной и ягодным соусом.','three golden round cottage cheese pancakes syrniki on ceramic plate with sour cream and berry compote',[sauce],['молоко','яйцо','глютен'])
add('breakfast','avocado-toast','Тост с авокадо',360,'240 г','Цельнозерновой хлеб, авокадо, томаты и яйцо пашот.','wholegrain sourdough toast topped with sliced avocado, poached egg and cherry tomatoes',[exclude('яйца','томатов')],['глютен','яйцо'])

add('coffee','vanilla-raf','Ванильный раф',320,'300 мл','Эспрессо, сливки и натуральная ваниль. Нежная воздушная текстура.','tall clear glass of creamy vanilla raf coffee, thick whipped cream, vanilla bean and tiny caramel crumbs, pale sage green stone surface',[coffee_size,milk,sweet,shot],['молоко'],True)
add('coffee','chocolate-mocha','Шоколадный мокко',340,'300 мл','Эспрессо, молоко, тёмный шоколад и взбитые сливки.','tall ceramic cup of chocolate mocha coffee topped with whipped cream, chocolate curls and chocolate drizzle',[coffee_size,milk,sweet,shot],['молоко'])
add('coffee','espresso','Эспрессо',180,'40 мл','Насыщенный эспрессо из свежеобжаренного зерна.','small demitasse white ceramic espresso cup filled with rich dark espresso and thick golden crema',[sizes('Порция',[('single','40 мл',0),('double','80 мл',60)],'single')])
add('coffee','americano','Американо',220,'250 мл','Двойной эспрессо и горячая вода. Чистый кофейный вкус.','black ceramic cup of black americano coffee, light steam, saucer and a few coffee beans',[sizes('Объём',[('250','250 мл',0),('350','350 мл',40)],'250'),sweet])
add('coffee','cappuccino','Капучино',300,'300 мл','Эспрессо, молоко и бархатная молочная пена.','speckled wide ceramic cappuccino cup on saucer with beautiful white rosetta latte art',[coffee_size,milk,sweet,shot],['молоко'])
add('coffee','latte','Латте',300,'300 мл','Мягкий эспрессо с большим количеством молока.','tall clear glass of cafe latte with creamy layered coffee and delicate latte art',[coffee_size,milk,sweet,shot],['молоко'])
add('coffee','flat-white','Флэт уайт',290,'200 мл','Двойной эспрессо и тонкий слой молочной пены.','small matte sage ceramic flat white coffee cup with fine microfoam heart latte art',[milk,sweet,shot],['молоко'])
add('coffee','iced-latte','Айс-латте',320,'400 мл','Охлаждённый эспрессо, молоко и кубики льда.','tall clear glass iced latte with milk espresso swirling and large clear ice cubes',[milk,sweet,shot],['молоко'])
add('coffee','cold-brew','Колд-брю',280,'300 мл','Кофе длительного холодного заваривания со льдом.','clear tumbler of dark amber cold brew black coffee over ice cubes, coffee beans beside')
add('coffee','espresso-tonic','Эспрессо-тоник',340,'350 мл','Эспрессо, тоник, лёд и долька апельсина.','tall glass espresso tonic, golden bubbly tonic at bottom and dark espresso layer above, orange slice and ice')

add('tea','black-tea','Чёрный чай',190,'400 мл','Листовой чёрный чай с насыщенным ароматом.','clear glass teapot and small cup of amber black tea with loose tea leaves',[sweet])
add('tea','jasmine-tea','Жасминовый чай',210,'400 мл','Зелёный чай с нежным ароматом жасмина.','clear glass teapot of pale golden green jasmine tea with small white jasmine flowers')
add('tea','sea-buckthorn','Облепиховый чай',290,'400 мл','Облепиха, апельсин, мёд и розмарин.','clear glass teapot of bright orange sea buckthorn tea, orange slices, sea buckthorn berries and rosemary',[sweet])
add('tea','ginger-lemon','Имбирно-лимонный чай',270,'400 мл','Свежий имбирь, лимон и мёд.','glass mug of warm ginger lemon tea with lemon slices, ginger root and honey',[sweet])
add('tea','matcha-latte','Матча-латте',330,'300 мл','Японская матча и тёплое молоко.','ceramic sage green cup of vibrant green matcha latte with white foam art and bamboo whisk beside',[milk,sweet],['молоко'])
add('tea','cocoa','Классическое какао',270,'300 мл','Какао, молоко и немного тёмного шоколада.','round ceramic mug of hot chocolate cocoa with light milk foam and dark chocolate shavings',[milk,sweet],['молоко'])

add('cold','cherry-lemonade','Вишнёвый лимонад',290,'400 мл','Вишня, лимон, газированная вода и мята.','tall glass of ruby red cherry lemonade with cherries, lemon slice, mint and ice',[sweet],popular=True)
add('cold','berry-iced-tea','Холодный ягодный чай',260,'400 мл','Чёрный чай, малина, смородина и лёд.','tall clear glass of deep ruby iced berry tea with raspberries, blackcurrants and ice',[sweet])
add('cold','lemon-mint','Лимонад лимон-мята',270,'400 мл','Свежий лимон, мята и газированная вода.','clear tall glass sparkling lemon mint lemonade, yellow lemon slices, lots of green mint and ice',[sweet])
add('cold','mango-passion','Манго-маракуйя',310,'400 мл','Манго, маракуйя и газированная вода.','tall glass of golden orange mango passionfruit lemonade, passion fruit half and mango cubes',[sweet])
add('cold','peach-iced-tea','Персиковый айс-ти',270,'400 мл','Чёрный чай с персиком, лимоном и льдом.','tall glass amber peach iced tea with peach slices lemon and clear ice',[sweet])
add('cold','orange-juice','Апельсиновый сок',290,'250 мл','Свежевыжатый апельсиновый сок без добавленного сахара.','clear glass of fresh orange juice with fresh cut juicy orange halves on the table')
add('cold','berry-smoothie','Ягодный смузи',340,'300 мл','Малина, черника, банан и яблочный сок.','tall glass of thick deep purple berry banana smoothie topped with raspberries and blueberries')
add('cold','tropical-smoothie','Тропический смузи',350,'300 мл','Манго, ананас, банан и апельсиновый сок.','tall glass of thick golden tropical mango pineapple banana smoothie with pineapple wedge and mango cubes')

for id,name,price,desc,photo in [
('vanilla-shake','Ванильный коктейль',320,'Молоко, ванильное мороженое и взбитые сливки.','vanilla milkshake in a tall clear glass with whipped cream and a vanilla bean'),
('chocolate-shake','Шоколадный коктейль',340,'Молоко, шоколадное мороженое и шоколадный соус.','rich brown chocolate milkshake in a tall glass with whipped cream and chocolate drizzle'),
('strawberry-shake','Клубничный коктейль',340,'Молоко, клубника, мороженое и взбитые сливки.','pink strawberry milkshake in a tall clear glass topped with whipped cream, strawberries and berry crumbs'),
('caramel-shake','Солёная карамель',350,'Молоко, мороженое, солёная карамель и сливки.','caramel milkshake in clear tall glass with whipped cream, golden caramel sauce and tiny sea salt flakes')]:
    add('shakes',id,name,price,'400 мл',desc,photo,[sweet],['молоко'])
add('shakes','mojito','Мохито без алкоголя',310,'400 мл','Лайм, мята, тростниковый сироп и газированная вода.','tall glass of nonalcoholic mojito with fresh lime wedges, green mint, crushed ice and soda',[sweet])
add('shakes','pina-colada','Пина-колада без алкоголя',350,'400 мл','Ананасовый сок, кокосовые сливки и лёд.','creamy white nonalcoholic pina colada in a curved clear glass, pineapple wedge and coconut flesh')

add('sandwiches','bbq-chicken','Сэндвич BBQ',390,'280 г','Куриное филе, соус BBQ, салат, томаты и хрустящий хлеб.','thick grilled toasted chicken BBQ sandwich cut diagonally, juicy chicken, lettuce and tomatoes, toasted sourdough on a wooden board with dark forest green background',[sand_size,cheese,exclude('лука','томатов'),sauce],['глютен'],True)
add('sandwiches','roastbeef-mustard','Ростбиф–горчица',420,'260 г','Ростбиф, горчичный соус, рукола и маринованный лук.','grilled sourdough roast beef sandwich with pink sliced roastbeef, arugula, pickled red onion and mustard sauce',[sand_size,cheese,exclude('лука'),sauce],['глютен','горчица'])
add('sandwiches','cheese-mushroom','Горячий сэндвич с сыром и грибами',390,'260 г','Хлеб на гриле, шампиньоны, сыр и сливочный соус.','grilled cheese sandwich cut diagonally with gooey melted cheese and sauteed mushrooms in toasted bread',[sand_size,exclude('соуса')],['глютен','молоко'])
add('sandwiches','caprese-panini','Панини капрезе',360,'250 г','Панини, моцарелла, томаты, базилик и песто.','grilled panini sandwich filled with fresh mozzarella, red tomatoes, basil and pesto',[exclude('томатов'),sauce],['глютен','молоко','орехи'])
add('sandwiches','chicken-wrap','Ролл с курицей',350,'250 г','Тортилья, курица-гриль, овощи и йогуртовый соус.','chicken wrap tortilla cut in halves showing grilled chicken, green lettuce, tomatoes and yogurt sauce',[exclude('томатов'),sauce],['глютен','молоко'])
add('sandwiches','salmon-toast','Тост с лососем',460,'220 г','Поджаренный хлеб, слабосолёный лосось, сливочный сыр и огурец.','open sourdough toast with smoked salmon folds, cream cheese, cucumber ribbons and dill',[exclude('огурца')],['глютен','молоко','рыба'])
add('sandwiches','cheese-sticks','Сырные палочки',320,'180 г','Сыр в хрустящей панировке с томатным соусом.','golden breaded mozzarella cheese sticks with one broken showing melting cheese and small tomato dip',[sauce],['глютен','молоко','яйцо'])
add('sandwiches','potato-wedges','Картофельные дольки',240,'200 г','Запечённый картофель с паприкой и чесночным соусом.','golden rustic roasted potato wedges with paprika, herbs and a small bowl of garlic dip',[sauce],['молоко'])

salad_groups = [group('dressing','Заправка',[choice('standard','Стандартная'),choice('apart','Отдельно'),choice('none','Без заправки')]),exclude('томатов')]
add('salads','caesar-chicken','Цезарь с курицей',360,'250 г','Салат романо, куриная грудка, пармезан, сухарики и соус цезарь.','dark ceramic bowl of chicken Caesar salad, romaine lettuce, sliced grilled chicken breast, parmesan shavings and golden croutons',[salad_groups[0],exclude('сухариков','пармезана')],['глютен','молоко','яйцо','рыба'],True)
add('salads','warm-beef-salad','Тёплый салат с говядиной',520,'240 г','Нежная говядина, овощи-гриль, микс салата и соус бальзамик.','dark ceramic bowl of warm beef salad with grilled beef slices, roasted peppers cherry tomatoes and mixed greens',salad_groups)
add('salads','grilled-chicken-salad','Салат с курицей-гриль',420,'230 г','Курица-гриль, микс салата, свежие овощи и ореховый соус.','bowl of fresh mixed lettuce with sliced chargrilled chicken, tomatoes cucumber and nut dressing',salad_groups,['орехи'])
add('salads','salmon-bowl','Боул с лососем',590,'280 г','Лосось, киноа, авокадо, эдамаме, овощи и соус понзу.','dark bowl with cooked salmon fillet, quinoa, avocado slices, edamame, cucumber and cherry tomatoes',salad_groups,['рыба','соя'])
add('salads','chicken-bowl','Боул с курицей',450,'300 г','Курица-гриль, рис, авокадо, кукуруза и свежие овощи.','bowl with grilled chicken strips, rice, sliced avocado, sweetcorn, tomato and cucumber',salad_groups)
add('salads','vegetable-bowl','Овощной боул',390,'270 г','Киноа, авокадо, овощи-гриль, нут и соус тахини.','colorful dark bowl of quinoa, chickpeas, avocado, grilled zucchini, roasted carrots and tahini dressing',salad_groups,['кунжут'])
add('salads','shrimp-avocado','Салат с креветками и авокадо',560,'240 г','Креветки, авокадо, черри, микс салата и цитрусовая заправка.','bowl of fresh salad with large pink cooked shrimp, avocado slices, cherry tomatoes and green leaves',salad_groups,['ракообразные'])
add('salads','greek-salad','Греческий салат',360,'250 г','Томаты, огурцы, фета, оливки, сладкий перец и орегано.','Greek salad of chunky tomatoes, cucumber, white feta cubes, black olives, sweet peppers and oregano',[salad_groups[0],exclude('лука','феты')],['молоко'])

hot_groups=[sauce,exclude('соуса')]
add('hot','chicken-steak','Куриный стейк',490,'320 г','Куриное филе на гриле, запечённые овощи и соус с травами.','juicy grilled chicken breast steak with grill marks, roasted vegetables potatoes and herb sauce on a ceramic plate',hot_groups)
add('hot','pork-steak','Свиной стейк',590,'350 г','Свиной стейк, картофельные дольки, овощи-гриль и перечный соус.','grilled pork steak with golden potato wedges, roasted vegetables and pepper sauce on a ceramic plate',hot_groups,['молоко'])
add('hot','beef-medallions','Говяжьи медальоны',790,'320 г','Говяжьи медальоны, картофель, овощи-гриль и демиглас.','three juicy grilled beef medallions with rich brown demi glace, roast potatoes and colorful grilled vegetables on dark plate',hot_groups)
add('hot','bbq-ribs','BBQ-рёбрышки',690,'400 г','Свиные рёбрышки в глазури BBQ, картофель и салат коул-слоу.','sticky glazed BBQ pork ribs on a wooden board with roasted potatoes and a small cabbage coleslaw',hot_groups,['яйцо'])
add('hot','stuffed-potato','Запечённый картофель с начинкой',390,'330 г','Запечённый картофель, курица, грибы, сыр и зелень.','large baked jacket potato split open filled with chicken, mushrooms and golden melted cheese, fresh dill',[exclude('сыра','грибов')],['молоко'])
add('hot','salmon-vegetables','Лосось с овощами',790,'300 г','Филе лосося, сезонные овощи и лимонный соус.','seared salmon fillet with crispy edges, roasted broccoli zucchini cherry tomatoes and lemon wedge on ceramic plate',hot_groups,['рыба','молоко'])
add('hot','chicken-pasta','Паста с курицей и грибами',490,'320 г','Тальятелле, курица, шампиньоны, сливочный соус и пармезан.','bowl of tagliatelle pasta in creamy sauce with grilled chicken pieces, mushrooms and parmesan',[exclude('пармезана')],['глютен','молоко'])
add('hot','shrimp-pasta','Паста с креветками',590,'300 г','Лингвини, креветки, томаты, чеснок и базилик.','linguine pasta with large pink shrimp, cherry tomatoes, garlic and fresh basil in a ceramic bowl',[exclude('чеснока')],['глютен','ракообразные'])
add('hot','chicken-rice','Рис с курицей и овощами',450,'330 г','Рис, курица, брокколи, сладкий перец и соус терияки.','bowl of rice with chicken pieces, broccoli, sweet peppers and glossy teriyaki sauce',[sauce],['соя'])

pizza_groups=[sizes('Диаметр',[('25','25 см',0),('30','30 см',180)],'25'),cheese,exclude('соуса')]
add('pizza','margherita','Пицца Маргарита',520,'25 см · 420 г','Тонкое тесто, томатный соус, моцарелла и базилик.','whole small margherita pizza with tomato sauce, melted mozzarella and fresh green basil on a wooden pizza board',pizza_groups,['глютен','молоко'])
add('pizza','pepperoni','Пицца Пепперони',590,'25 см · 450 г','Тонкое тесто, томатный соус, моцарелла и пепперони.','whole pepperoni pizza with red pepperoni slices, tomato sauce and golden melted mozzarella on wooden pizza board',pizza_groups,['глютен','молоко'])
add('pizza','four-cheese','Пицца Четыре сыра',620,'25 см · 430 г','Моцарелла, чеддер, пармезан, голубой сыр и сливочный соус.','whole four cheese pizza with creamy golden melted mozzarella cheddar parmesan and blue cheese on wooden board',pizza_groups,['глютен','молоко'])
add('pizza','chicken-mushroom-pizza','Пицца Курица и грибы',590,'25 см · 470 г','Курица, шампиньоны, моцарелла и сливочный соус.','whole small pizza topped with chicken pieces, sliced mushrooms and golden melted mozzarella on a wooden board',pizza_groups,['глютен','молоко'])

add('desserts','chocolate-fondant','Шоколадный фондан',390,'180 г','Тёплый шоколадный кекс с жидкой серединой и ванильным мороженым.','dark chocolate fondant lava cake cut open with flowing melted chocolate, vanilla ice cream scoop and berries on ceramic plate',[],['глютен','яйцо','молоко'])
add('desserts','caramel-cheesecake','Карамельный чизкейк',320,'160 г','Сливочный чизкейк на песочной основе с солёной карамелью.','single triangular slice of creamy caramel cheesecake with glossy caramel dripping over sides on white ceramic plate',[],['глютен','яйцо','молоко'],True)
add('desserts','apple-crumble','Яблочный крамбл',310,'190 г','Тёплые яблоки с корицей, рассыпчатая крошка и ванильное мороженое.','small ceramic dish of baked apple crumble with golden crumb topping, cinnamon and vanilla ice cream scoop',[],['глютен','молоко'])
add('desserts','honey-cake','Медовик',340,'150 г','Тонкие медовые коржи и нежный сметанный крем.','triangular slice of Russian honey cake medovik with many thin golden layers and cream on ceramic plate',[],['глютен','яйцо','молоко'])
add('desserts','berry-tart','Ягодная тарталетка',290,'130 г','Песочная корзинка, заварной крем и свежие ягоды.','small round shortcrust berry tart with vanilla custard, fresh raspberries blueberries and strawberry halves',[],['глютен','яйцо','молоко'])
add('desserts','almond-croissant','Миндальный круассан',270,'120 г','Слоёный круассан с миндальным кремом и лепестками миндаля.','single golden flaky almond croissant topped with sliced almonds and a little powdered sugar on ceramic plate',[],['глютен','молоко','орехи'])
add('desserts','chocolate-cookie','Шоколадное печенье',170,'90 г','Мягкое печенье с кусочками тёмного шоколада.','two thick rustic chocolate chip cookies with visible dark chocolate chunks on a small ceramic plate',[],['глютен','яйцо','молоко'])
add('desserts','tiramisu','Тирамису',350,'160 г','Савоярди, эспрессо, крем маскарпоне и какао. Без алкоголя.','square portion of tiramisu with distinct coffee soaked sponge and mascarpone layers, cocoa dusting on top on white plate',[],['глютен','яйцо','молоко'])

data=dict(categories=[dict(id=id,name=name,sortOrder=i) for i,(id,name) in enumerate(CATS)],products=products)
dest=ROOT/'content-staging/catalog-full.json'
dest.parent.mkdir(parents=True,exist_ok=True)
dest.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# Демокаталог Mellow House','', 'Цены и рецептуры демонстрационные. Источник редактирования: `src/MellowHouse.Web/Data/catalog.json`. Все обязательные позиции сохранены.','', '| Категория | Товар | Порция | Цена, ₽ |','|---|---|---|---:|']
names=dict(CATS)
for p in products: lines.append(f"| {names[p['categoryId']]} | {p['name']} | {p['portion']} | {p['price']} |")
(ROOT/'content-staging/CATALOG-FULL.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(f'Создано {len(products)} товаров в {len(CATS)} категориях')
