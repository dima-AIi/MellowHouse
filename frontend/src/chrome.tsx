import { useEffect, useState } from "react";
import { Icon, money, type View } from "./components";
export function Header({
  view,
  authenticated = false,
  menuOpen = false,
  connecting = false,
}: {
  view: View;
  authenticated?: boolean;
  menuOpen?: boolean;
  connecting?: boolean;
}) {
  return (
    <>
      <button
        className="icon-btn hamburger"
        aria-label="Открыть навигацию"
        aria-expanded={menuOpen}
        aria-controls="mobile-nav"
        aria-haspopup="dialog"
        id="mobile-menu-button"
      >
        <Icon name="menu" />
      </button>
      <a className="brand" href="/" translate="no">
        Mellow House
      </a>
      <nav className="desktop-nav" aria-label="Основная навигация">
        {[
          ["/", "Главная"],
          ["/Menu", "Меню"],
          ["/Contacts", "Контакты"],
        ].map(([href, label]) => (
          <a
            key={href}
            href={href}
            className={
              location.pathname.toLowerCase() === href!.toLowerCase()
                ? "active"
                : undefined
            }
            aria-current={
              location.pathname.toLowerCase() === href!.toLowerCase()
                ? "page"
                : undefined
            }
          >
            {label}
          </a>
        ))}
      </nav>
      <div className="header-actions">
        <a
          href="/App#install-instructions"
          className="header-app"
          aria-label="Установить Mellow House"
          title="Как установить приложение"
        >
          <Icon name="app-window" />
          <span>Приложение</span>
        </a>
        <button
          className="header-cart"
          data-open-cart=""
          aria-busy={connecting || undefined}
          aria-label={
            connecting && !view.cart.count
              ? "Открыть корзину"
              : `Открыть корзину, ${view.cart.count} шт., ${money(view.cart.subtotal)}`
          }
        >
          <Icon name="bag" />
          <span data-cart-total="">
            {connecting && !view.cart.count
              ? "Корзина"
              : money(view.cart.subtotal)}
          </span>
          <small data-cart-count="" hidden={!view.cart.count}>
            {view.cart.count}
          </small>
        </button>
        <button
          className="header-account"
          data-account=""
          aria-label={authenticated ? "Профиль" : "Войти"}
        >
          <Icon name="user" />
          <span data-account-label="">
            {authenticated ? "Профиль" : "Войти"}
          </span>
        </button>
      </div>
    </>
  );
}
const groups = [
  {
    name: "Меню",
    links: [
      ["/Menu#breakfast", "Завтраки"],
      ["/Menu#sandwiches", "Сэндвичи и закуски"],
      ["/Menu#salads", "Салаты и боулы"],
      ["/Menu#hot", "Горячие блюда"],
      ["/Menu#pizza", "Пицца"],
      ["/Menu#desserts", "Десерты и выпечка"],
      ["/Menu#coffee", "Кофе и напитки"],
    ],
  },
  {
    name: "О нас",
    links: [
      ["/About", "Наша история"],
      ["/About#ingredients", "Кофе и ингредиенты"],
      ["/Contacts", "Контакты"],
      ["/Account", "Личный кабинет"],
    ],
  },
  {
    name: "Доставка и оплата",
    links: [
      ["/Delivery", "Условия доставки"],
      ["/Delivery#payment", "Способы оплаты"],
      ["/Delivery#faq", "Вопросы и ответы"],
      ["/Legal", "Правовая информация"],
      ["/Legal#privacy", "Конфиденциальность"],
    ],
  },
];
export function Footer() {
  const [desktop, setDesktop] = useState(
    () => matchMedia("(min-width:701px)").matches,
  );
  useEffect(() => {
    const query = matchMedia("(min-width:701px)"),
      changed = () => setDesktop(query.matches);
    query.addEventListener("change", changed);
    return () => query.removeEventListener("change", changed);
  }, []);
  return (
    <>
      <div>
        <a className="brand" href="/">
          Mellow House
        </a>
        <p>
          Хорошая еда. Яркие люди.
          <br />
          Каждый день.
        </p>
        <a href="/Allergens">Состав и аллергены</a>
      </div>
      {groups.map((group) => (
        <details
          key={group.name}
          className="footer-links"
          open={desktop ? true : undefined}
        >
          <summary>
            <span>{group.name}</span>
            <Icon name="chevron-down" />
          </summary>
          <div>
            {group.links.map(([href, label]) => (
              <a href={href} key={href}>
                {label}
              </a>
            ))}
          </div>
        </details>
      ))}
      <div className="footer-app">
        <h2>Приложения Mellow House</h2>
        <div className="platform-badges" aria-label="Будущие приложения">
          {["iPhone", "Android", "Компьютер"].map((platform) => (
            <button
              type="button"
              className="platform-badge"
              data-platform-preview={
                platform === "Компьютер" ? "компьютера" : platform
              }
              key={platform}
            >
              <Icon name={platform === "Компьютер" ? "app-window" : "phone"} />
              <span>
                {platform}
                <small>Скоро</small>
              </span>
            </button>
          ))}
        </div>
        <p className="platform-note" id="platform-note" role="status" />
        <a className="footer-install" href="/App#install-instructions">
          <Icon name="app-window" />
          <span>Добавить на главный экран</span>
        </a>
      </div>
    </>
  );
}
