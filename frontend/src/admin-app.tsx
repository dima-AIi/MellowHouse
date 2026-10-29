import { useEffect, useState } from "react";
import { ShopStore, useShop } from "./store";
import { Auth } from "./cart-auth";
import { AdminCatalogPage } from "./admin-catalog";
import { AdminOrders } from "./admin-orders";
import { AdminReport } from "./admin-report";
import { AdminClients, AdminSettingsPage } from "./admin-people";
import { Icon } from "./components";
import { useAction } from "./ui";
const sections = [
  ["overview", "Обзор и отчёты", "clock"],
  ["catalog", "Каталог", "bag"],
  ["orders", "Заказы", "truck"],
  ["clients", "Гости и команда", "user"],
  ["settings", "Настройки", "app-window"],
];
export function AdminApp({ store }: { store: ShopStore }) {
  useShop(store);
  const [open, setOpen] = useState(false),
    action = useAction(),
    section =
      location.pathname.split("/").filter(Boolean)[1]?.toLowerCase() ??
      "overview",
    login = section === "login";
  useEffect(() => {
    document.body.classList.toggle("admin-nav-open", open);
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("keydown", escape);
    return () => document.removeEventListener("keydown", escape);
  }, [open]);
  if (login)
    return (
      <main className="admin-login-main">
        <Auth store={store} admin />
      </main>
    );
  if (store.phase !== "connected")
    return (
      <main className="admin-main">
        <h1>Подключаем панель управления…</h1>
        <p role="status">
          {store.phase === "failed"
            ? "Не удалось связаться с сервером. Проверьте соединение."
            : "Загружаем данные кафе."}
        </p>
        <button className="btn" onClick={() => location.reload()}>
          Повторить
        </button>
      </main>
    );
  if (store.app.state?.profile?.role !== "Admin")
    return (
      <main className="admin-login-main">
        <Auth store={store} admin />
      </main>
    );
  return (
    <>
      <a className="skip-link" href="#admin-main">
        Перейти к содержимому
      </a>
      <aside className="admin-sidebar" id="admin-sidebar">
        <a className="brand" href="/Admin">
          Mellow House
        </a>
        <p className="admin-kicker">Рабочее пространство</p>
        <nav aria-label="Панель управления">
          {sections.map(([key, label, icon]) => (
            <a
              className={section === key ? "active" : ""}
              aria-current={section === key ? "page" : undefined}
              href={"/Admin/" + key}
              key={key}
              onClick={() => setOpen(false)}
            >
              <Icon name={icon!} />
              <span>{label}</span>
            </a>
          ))}
        </nav>
        <div className="admin-sidebar-bottom">
          <a href="/">Открыть сайт</a>
          <button
            className="text-button"
            disabled={action.busy}
            onClick={() =>
              void action.run(async () => {
                await store.logout();
                location.href = "/Admin/Login";
              })
            }
          >
            Выйти
          </button>
        </div>
      </aside>
      <header className="admin-topbar">
        <button
          className="icon-btn"
          aria-label="Открыть навигацию"
          aria-controls="admin-sidebar"
          aria-expanded={open}
          onClick={() => setOpen(!open)}
        >
          <Icon name="menu" />
        </button>
        <span>
          Mellow House /{" "}
          <strong>
            {sections.find(([key]) => key === section)?.[1] ?? "Обзор и отчёты"}
          </strong>
        </span>
        <a href="/" className="admin-site-link">
          Открыть сайт
        </a>
      </header>
      <main id="admin-main" className="admin-main">
        <div id="admin-workspace" data-section={section}>
          {section === "catalog" ? (
            <AdminCatalogPage store={store} />
          ) : section === "orders" ? (
            <AdminOrders store={store} />
          ) : section === "clients" ? (
            <AdminClients store={store} />
          ) : section === "settings" ? (
            <AdminSettingsPage store={store} />
          ) : (
            <AdminReport store={store} />
          )}
        </div>
        {action.error && <p className="form-error">{action.error}</p>}
      </main>
      {store.message && (
        <div className="toast" role="status">
          <span>{store.message}</span>
        </div>
      )}
    </>
  );
}
