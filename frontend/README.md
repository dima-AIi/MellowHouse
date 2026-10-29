# React + TypeScript storefront

Интерфейс подключается к текущему ASP.NET Core приложению через Vite. Это постепенный перенос с сохранением ссылок, серверного HTML, CSS и действующих контрактов API.

## Запуск

Из корня репозитория, Node.js 24 и pnpm 11.25.0:

```powershell
pnpm --dir frontend install --frozen-lockfile
pnpm --dir frontend build
dotnet run --project src/MellowHouse.Web
```

При работе над компонентами `pnpm --dir frontend dev` пересобирает их при сохранении. ASP.NET Core обслуживает результат из `wwwroot/react`; отдельный origin для cookies, CSRF и API не нужен. Проверки: `pnpm --dir frontend check`, `pnpm --dir frontend test`.

## Границы переноса

`components.tsx` — каталог, карточки, поиск, КБЖУ, FAQ и настройки. `assistant.tsx` — диалог, история, повтор сообщения и конфигурация. `chrome.tsx` — шапка и подвал. `bridge.tsx` — явные границы владения DOM между React и существующими контроллерами.

Типы DTO и запросов определены в `types.ts`; `strict` и `noUncheckedIndexedAccess` включены. `legacy.d.ts` описывает существующий API модуля, но не объявляет старые JavaScript-файлы переведёнными на TypeScript. В сборке остаётся ровно один экземпляр `core.js`, поэтому корзина, cookies и авторизация общие.

Авторизация, корзина, оформление, платёжные страницы, история и админка пока используют существующие серверные страницы/контроллеры. Их API и схема базы не менялись. Это завершённый первый этап, а не полный перевод всех экранов на React.

Docker и Vercel собирают исходники с lockfile. Готовые bundles и node_modules не входят в Git. Если bundle не скачался, bootstrap подключает прежний интерфейс. Без JavaScript остаётся серверная разметка меню.
