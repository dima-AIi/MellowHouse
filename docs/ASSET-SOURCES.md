# Источники изображений

Основные фотографии созданы встроенным image_gen для Mellow House; лицензированный ролик и его poster описаны отдельно ниже. PNG исходники сохранены в `artifacts/assets-original/`; промпты — в соседних JSON и `docs/asset-manifest.json`. WebP — только оптимизация размера, без изменения содержания. У двух товарных изображений проверен настоящий альфа-канал.

| Файл | Назначение | Источник / условия |
|---|---|---|
| `images/products/vanilla-raf.webp` | Ванильный раф | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/products/bbq-chicken.webp` | Сэндвич BBQ с курицей на тарелке | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/hero-v2.webp` | Главная: кофе, сэндвич, салат, горячее | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/hero-mobile-v2.webp` | Главная, мобильная версия | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/barista.webp` | Бариста за приготовлением кофе | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/menu-banner.webp` | Капучино и карамельный чизкейк, рекламный блок | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/categories/egg-croissant.webp` | Фотокатегория: egg-croissant | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/categories/roastbeef-mustard.webp` | Фотокатегория: roastbeef-mustard | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/categories/salmon-bowl.webp` | Фотокатегория: salmon-bowl | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/categories/beef-medallions.webp` | Фотокатегория: beef-medallions | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/categories/cappuccino.webp` | Фотокатегория: cappuccino | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/categories/cherry-lemonade.webp` | Фотокатегория: cherry-lemonade | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/categories/strawberry-shake.webp` | Фотокатегория: strawberry-shake | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/categories/chocolate-fondant.webp` | Фотокатегория: chocolate-fondant | Собственная генерация built-in image_gen; Сгенерировано для проекта; чужие фотографии не публикуются |
| `images/pwa-192.png` | Иконка Mellow House | Собственная геометрическая эмблема M; Создана кодом в проекте |
| `images/pwa-512.png` | Иконка Mellow House | Собственная геометрическая эмблема M; Создана кодом в проекте |

## Референсные сайты

- Joe: https://www.joejuice.com/policy/terms-of-use — фотографии защищены, публичный перенос без явного разрешения не допускается.
- Starbucks: https://www.starbucks.com/terms/starbucks-terms-of-use и https://about.starbucks.com/multimedia/2022/sbx20220815-coffeecommitment-cafepractices/ — медиа не используются для коммерческого каталога.
- Pret: https://www.pret.co.uk/en-GB/legal — права принадлежат Pret, разрешение для нашего меню не получено.
- Costa: https://www.costa.co.uk/menu — разрешение на использование фотографий для Mellow House не подтверждено.
- Dolce Vita: https://dolcevitacafe.ru/ — отдельное разрешение на фотографии не предоставлено.
- Из этих сайтов и из макетов фотографии в наш сайт не скачивались. Использовано только изучение композиции и поведения.

## Шрифты

Oswald и Golos Text: официальный https://github.com/google/fonts, SIL Open Font License 1.1. TTF и полные OFL-лицензии находятся в `wwwroot/fonts/`. SVG-иконки написаны в проекте.

## Отложенное наполнение

Предварительные фото полного ассортимента сохранены отдельно и не подключены как товары. Их соответствие рецептурам и пригодность нужно проверить на финальном этапе.

## Редакция 2: рекламное видео меню

| Файл | Назначение | Источник и условия |
|---|---|---|
| `wwwroot/videos/coffee.mp4` | Приготовление кофе в начале меню | Pavel Danilyuk, Pexels video 6602227: https://www.pexels.com/video/barista-pouring-milk-on-coffee-6602227/. Pexels License https://www.pexels.com/license/ разрешает использование на сайтах и модификацию; авторство указано добровольно. Ролик не выдаётся за съёмку сотрудников Mellow House. |
| `wwwroot/images/coffee-video-poster.webp` | Poster ролика | Кадр того же разрешённого видео; те же условия. |

Официальный исходник загружен с videos.pexels.com. Локальная оптимизация FFmpeg 7.1: фрагмент 15 секунд, 720 px по ширине, H.264, без звука, faststart; 951 501 байт. Poster — 17 198 байт. Исходник сохранён отдельно в игнорируемом artifacts/assets-original/. Imagegen для видео и QR не использовался. Чужие фирменные фотографии и упаковка не добавлялись.


## 20.10.2026 — 30 новых товаров
Для каждой позиции создана отдельная собственная фотография через встроенный image_gen: нейтральная посуда, мягкое студийное освещение, прозрачный фон. Чужие фотографии, упаковка и логотипы не использовались. Полный список с путями исходников и условиями — asset-manifest.json; проверка файлов — artifacts/ASSETS-V3-RESULTS.json. Оригиналы сохранены в artifacts/assets-original/*-v3-cutout.png. Локальные PNG оптимизированы до 840×640 с сохранением RGBA. У боула с лососем изображение отдельно исправлено: киноа вместо риса в соответствии с рецептурой.

| Товар | Локальное изображение | Источник / условия |
|---|---|---|
| Сырники | `wwwroot/images/products/syrniki.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Круассан с яйцом | `wwwroot/images/products/egg-croissant.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Тост с авокадо | `wwwroot/images/products/avocado-toast.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Омлет с беконом | `wwwroot/images/products/bacon-omelette.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Эспрессо | `wwwroot/images/products/espresso.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Американо | `wwwroot/images/products/americano.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Капучино | `wwwroot/images/products/cappuccino.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Латте | `wwwroot/images/products/latte.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Шоколадный мокко | `wwwroot/images/products/chocolate-mocha.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Чёрный чай | `wwwroot/images/products/black-tea.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Матча-латте | `wwwroot/images/products/matcha-latte.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Облепиховый чай | `wwwroot/images/products/sea-buckthorn.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Вишнёвый лимонад | `wwwroot/images/products/cherry-lemonade.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Апельсиновый сок | `wwwroot/images/products/orange-juice.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Айс-латте | `wwwroot/images/products/iced-latte.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Клубничный коктейль | `wwwroot/images/products/strawberry-shake.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Ванильный коктейль | `wwwroot/images/products/vanilla-shake.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Горячий сэндвич с сыром и грибами | `wwwroot/images/products/cheese-mushroom.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Тост с лососем | `wwwroot/images/products/salmon-toast.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Цезарь с курицей | `wwwroot/images/products/caesar-chicken.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Боул с лососем | `wwwroot/images/products/salmon-bowl.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Греческий салат | `wwwroot/images/products/greek-salad.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Говяжьи медальоны | `wwwroot/images/products/beef-medallions.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Паста с курицей и грибами | `wwwroot/images/products/chicken-pasta.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Лосось с овощами | `wwwroot/images/products/salmon-vegetables.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Пицца Маргарита | `wwwroot/images/products/margherita.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Пицца Пепперони | `wwwroot/images/products/pepperoni.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Шоколадный фондан | `wwwroot/images/products/chocolate-fondant.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Карамельный чизкейк | `wwwroot/images/products/caramel-cheesecake.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |
| Миндальный круассан | `wwwroot/images/products/almond-croissant.png` | Собственная генерация image_gen для Mellow House; RGBA PNG |

Фирменный знак `wwwroot/images/mellow-mark.svg` — собственный векторный рисунок дома с чашкой кофе. Значки платформ — простые векторные обозначения устройств; внешние магазины и скачивания не подключены.


## Исправление видео по отзыву 20.10.2026
`wwwroot/videos/coffee-full-v4.mp4` — полный исходник Pavel Danilyuk / Pexels 6602227 (ссылка и Pexels License приведены выше), без обрезания финала. Кадрирование исходного 1080×1920 в 1080×1080 на руки и чашку, H.264 CRF 18, без звука, faststart; в конце 3 секунды готового напитка. Длительность в браузере 34,568 с, размер 15 535 572 байта. Разрешение не повышалось искусственно.
`wwwroot/images/coffee-final-v4.webp` — последний кадр того же разрешённого видео, готовая чашка, WebP 90. Исходник сохранён отдельно. Старые файлы не удалены; штатные настройки переводятся на полную версию при запуске.

## Итерация 4
`images/mellow-emblem-v4.svg` — собственная векторная монограмма кафе, создана в проекте, без чужих логотипов. `videos/coffee-full-v5.mp4` — тот же разрешённый исходник Pavel Danilyuk/Pexels6602227, 1080×1080, H.264 CRF18, полный процесс 31,565 с, без tpad/замирания финала. Исходник и предыдущие варианты сохранены.

## Завершение меню — 21.10.2026

46 новых собственных RGBA PNG через встроенный image_gen. По последовательным уточнениям пользователя 37 готовых новых изображений оставлены побайтно прежними; последние 9 используют исходные иллюстрации как референсы фактуры, света и формы посуды, со спокойными цветами и простой подачей. Это сгенерированные изображения, не фотографии реальной кухни. Старые 32 фотографии не заменялись. Правила — PRODUCT-PHOTOGRAPHY.md, точные промпты/исходники — asset-manifest.json и artifacts/menu-generated-v6/.

| Товар | Файл | Стиль |
|---|---|---|
| Картофельные вафли | `/images/products/potato-waffles-v6.png` | Сохранён готовый |
| Гранола с ягодами | `/images/products/berry-granola-v6.png` | Сохранён готовый |
| Флэт уайт | `/images/products/flat-white-v6.png` | Сохранён готовый |
| Колд-брю | `/images/products/cold-brew-v6.png` | Сохранён готовый |
| Эспрессо-тоник | `/images/products/espresso-tonic-v6.png` | Сохранён готовый |
| Жасминовый чай | `/images/products/jasmine-tea-v6.png` | Сохранён готовый |
| Имбирно-лимонный чай | `/images/products/ginger-lemon-v6.png` | Сохранён готовый |
| Классическое какао | `/images/products/cocoa-v6.png` | Сохранён готовый |
| Холодный ягодный чай | `/images/products/berry-iced-tea-v6.png` | Сохранён готовый |
| Лимонад лимон-мята | `/images/products/lemon-mint-v6.png` | Сохранён готовый |
| Манго-маракуйя | `/images/products/mango-passion-v6.png` | Сохранён готовый |
| Персиковый айс-ти | `/images/products/peach-iced-tea-v6.png` | Сохранён готовый |
| Ягодный смузи | `/images/products/berry-smoothie-v6.png` | Сохранён готовый |
| Тропический смузи | `/images/products/tropical-smoothie-v6.png` | Сохранён готовый |
| Шоколадный коктейль | `/images/products/chocolate-shake-v6.png` | Сохранён готовый |
| Солёная карамель | `/images/products/caramel-shake-v6.png` | Сохранён готовый |
| Мохито без алкоголя | `/images/products/mojito-v6.png` | Сохранён готовый |
| Пина-колада без алкоголя | `/images/products/pina-colada-v6.png` | Сохранён готовый |
| Ростбиф–горчица | `/images/products/roastbeef-mustard-v6.png` | Сохранён готовый |
| Панини капрезе | `/images/products/caprese-panini-v6.png` | Сохранён готовый |
| Ролл с курицей | `/images/products/chicken-wrap-v6.png` | Сохранён готовый |
| Сырные палочки | `/images/products/cheese-sticks-v6.png` | Сохранён готовый |
| Картофельные дольки | `/images/products/potato-wedges-v6.png` | Сохранён готовый |
| Тёплый салат с говядиной | `/images/products/warm-beef-salad-v6.png` | Сохранён готовый |
| Салат с курицей-гриль | `/images/products/grilled-chicken-salad-v6.png` | Сохранён готовый |
| Боул с курицей | `/images/products/chicken-bowl-v6.png` | Сохранён готовый |
| Овощной боул | `/images/products/vegetable-bowl-v6.png` | Сохранён готовый |
| Салат с креветками и авокадо | `/images/products/shrimp-avocado-v6.png` | Сохранён готовый |
| Куриный стейк | `/images/products/chicken-steak-v6.png` | Сохранён готовый |
| Свиной стейк | `/images/products/pork-steak-v6.png` | Сохранён готовый |
| BBQ-рёбрышки | `/images/products/bbq-ribs-v6.png` | Сохранён готовый |
| Запечённый картофель с начинкой | `/images/products/stuffed-potato-v6.png` | Сохранён готовый |
| Паста с креветками | `/images/products/shrimp-pasta-v6.png` | Сохранён готовый |
| Рис с курицей и овощами | `/images/products/chicken-rice-v6.png` | Сохранён готовый |
| Пицца Четыре сыра | `/images/products/four-cheese-v6.png` | Сохранён готовый |
| Пицца Курица и грибы | `/images/products/chicken-mushroom-pizza-v6.png` | Сохранён готовый |
| Яблочный крамбл | `/images/products/apple-crumble-v6.png` | Естественная подача |
| Медовик | `/images/products/honey-cake-v6.png` | Естественная подача |
| Ягодная тарталетка | `/images/products/berry-tart-v6.png` | Естественная подача |
| Шоколадное печенье | `/images/products/chocolate-cookie-v6.png` | Естественная подача |
| Тирамису | `/images/products/tiramisu-v6.png` | Естественная подача |
| Клубничная матча-латте | `/images/products/strawberry-matcha-v6.png` | Естественная подача |
| Манговая матча-латте | `/images/products/mango-matcha-v6.png` | Сохранён готовый |
| Коктейль Бабл-гам | `/images/products/bubblegum-shake-v6.png` | Естественная подача |
| Бабл-ти Таро | `/images/products/taro-bubble-tea-v6.png` | Естественная подача |
| Бабл-ти Манго | `/images/products/mango-bubble-tea-v6.png` | Естественная подача |
