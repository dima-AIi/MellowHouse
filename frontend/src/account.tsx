import { useEffect, useState } from "react";
import { ShopStore, useShop } from "./store";
import type { CheckoutData, Order } from "./types";
import { Empty, Field, Form, useAction, useResource } from "./ui";
import {
  date,
  lastOrderUrl,
  orderUrl,
  paymentStatuses,
  today,
} from "./sessions";
import { image } from "./media";
import { money } from "./components";
import { closed, placed } from "./orders";
import { orderStatus } from "./order-state";
export function Account({ store }: { store: ShopStore }) {
  useShop(store);
  const profile = store.app.state?.profile,
    profileView = ["profile", "address"].includes(
      new URLSearchParams(location.search).get("view") ?? "",
    );
  const orders = useResource<Order[]>(
    () => (profile ? store.request("/api/orders") : Promise.resolve([])),
    profile?.id ?? "guest",
  );
  const action = useAction();
  async function repeat(id: string) {
    await action.run(async () => {
      const reply = await store.request<{
        cart: ReturnType<ShopStore["cartView"]>;
        warnings: string[];
      }>("/api/orders/" + id + "/repeat", {}, "POST");
      store.setCart(reply.cart);
      store.toast(
        reply.warnings.join(" ") || "Заказ добавлен в корзину по текущим ценам",
      );
      location.href = "/Menu?cart=1";
    });
  }
  return (
    <div className="container page-space" id="account-content">
      <div className="section-heading">
        <h1>Личный кабинет</h1>
        {profile && (
          <button
            className="text-button"
            onClick={() => void action.run(() => store.logout())}
          >
            Выйти
          </button>
        )}
      </div>
      <nav className="account-nav">
        <a href="/Account" className={!profileView ? "active" : ""}>
          История заказов
        </a>
        <a href="/Account?view=profile" className={profileView ? "active" : ""}>
          Личные данные
        </a>
        <a href="/Menu?tab=favorites#catalog-start">Избранное</a>
        {profile?.role === "Admin" && <a href="/Admin">Панель управления</a>}
      </nav>
      {!profile ? (
        <Empty
          title="Войдите в Mellow House"
          text="Здесь будет общая история профиля. Заказы без входа открываются по личной ссылке; корзина и избранное сохранены."
        >
          <button className="btn" onClick={() => store.openAuth()}>
            Войти
          </button>
          {lastOrderUrl() && (
            <a className="btn btn-outline" href={lastOrderUrl()!}>
              Открыть последний заказ без входа
            </a>
          )}
        </Empty>
      ) : profileView ? (
        <Profile store={store} />
      ) : (
        <>
          <h2>История заказов</h2>
          {orders.error && (
            <p className="form-error" role="alert">
              {orders.error}
            </p>
          )}
          {orders.value?.length ? (
            orders.value.map((order) => (
              <article className="history-card" key={order.id}>
                <div className="history-head">
                  <h3>Заказ {order.number}</h3>
                  <time dateTime={order.createdUtc}>
                    {date(order.createdUtc)}
                  </time>
                  <span className="status-pill">
                    {orderStatus(order).title}
                  </span>
                  <span className="small muted">
                    {paymentStatuses[order.status]}
                  </span>
                </div>
                <div className="history-content">
                  <div className="history-products">
                    {order.items.map((item, index) => (
                      <div className="history-product" key={index}>
                        <img
                          src={image(item.image)}
                          alt={item.name}
                          width="120"
                          height="85"
                          loading="lazy"
                        />
                        <strong>{item.name}</strong>
                        <small>
                          {item.summary.join(", ") || item.portion}
                          <br />
                          {money(item.unitPrice)} · {item.quantity} шт.
                        </small>
                      </div>
                    ))}
                  </div>
                  <div className="history-actions">
                    <strong>{money(order.total)}</strong>
                    <a
                      className="btn btn-outline"
                      href={orderUrl(
                        order.id,
                        placed(order) || closed(order) ? "Order" : "Payment",
                      )}
                    >
                      {placed(order) || closed(order)
                        ? "Подробнее"
                        : "Продолжить оплату"}
                    </a>
                    <button
                      className="btn btn-outline"
                      disabled={action.busy}
                      onClick={() => void repeat(order.id)}
                    >
                      Повторить заказ
                    </button>
                  </div>
                </div>
              </article>
            ))
          ) : orders.value ? (
            <Empty
              title="Первый заказ — впереди"
              text="Выберите любимые блюда, и они останутся в вашей истории."
            >
              <a className="btn" href="/Menu">
                Открыть меню
              </a>
            </Empty>
          ) : (
            <p role="status">Загружаем историю…</p>
          )}
        </>
      )}
      {action.error && (
        <p className="form-error" role="alert">
          {action.error}
        </p>
      )}
    </div>
  );
}
function Profile({ store }: { store: ShopStore }) {
  const profile = store.app.state!.profile!,
    action = useAction(),
    [data, setData] = useState({
      name: profile.name,
      birthDate: profile.birthDate ?? "",
      deliveryPhone: profile.deliveryPhone ?? "",
      city: "",
      street: "",
      apartment: "",
    }),
    [status, setStatus] = useState("");
  useEffect(() => {
    let active = true;
    store
      .request<Partial<CheckoutData>>("/api/checkout-draft")
      .then((draft) => {
        if (active)
          setData((previous) => ({
            ...previous,
            city: draft.city ?? "",
            street: draft.street ?? "",
            apartment: draft.apartment ?? "",
          }));
      })
      .catch((error) => action.setError(error.message));
    return () => {
      active = false;
    };
  }, [store]);
  async function save() {
    await action.run(async () => {
      await store.request(
        "/api/profile",
        { ...data, savePersonal: true, saveAddress: true },
        "PUT",
      );
      await store.refresh();
      setStatus("Личные данные сохранены.");
    });
  }
  return (
    <Form onSubmit={() => void save()} busy={action.busy} error={action.error}>
      <div className="profile-form">
        <h2>Личные данные</h2>
        <Field
          label="Имя"
          autoComplete="given-name"
          maxLength={80}
          value={data.name}
          onChange={(e) => setData({ ...data, name: e.target.value })}
        />
        <p className="small muted">Контакт для входа: {profile.contact}</p>
        <Field
          label="Дата рождения (необязательно)"
          type="date"
          autoComplete="bday"
          max={today()}
          value={data.birthDate}
          onChange={(e) => setData({ ...data, birthDate: e.target.value })}
        />
        <Field
          label="Телефон для заказа (необязательно)"
          type="tel"
          autoComplete="tel"
          value={data.deliveryPhone}
          onChange={(e) => setData({ ...data, deliveryPhone: e.target.value })}
        />
        <h3>Адрес доставки</h3>
        <p className="small muted">
          Сохранённый адрес подставится при оформлении. Адрес можно оставить
          пустым для самовывоза.
        </p>
        <Field
          label="Город"
          maxLength={80}
          value={data.city}
          onChange={(e) => setData({ ...data, city: e.target.value })}
        />
        <Field
          label="Улица и дом"
          maxLength={160}
          value={data.street}
          onChange={(e) => setData({ ...data, street: e.target.value })}
        />
        <Field
          label="Квартира"
          maxLength={30}
          value={data.apartment}
          onChange={(e) => setData({ ...data, apartment: e.target.value })}
        />
        <p role="status">{status}</p>
        <button className="btn" type="submit">
          Сохранить личные данные
        </button>
      </div>
    </Form>
  );
}
export function ResetPassword({ store }: { store: ShopStore }) {
  const [token] = useState(() => {
      const value = decodeURIComponent(location.hash.slice(1));
      history.replaceState(null, "", location.pathname);
      return value;
    }),
    [password, setPassword] = useState(""),
    [confirmation, setConfirmation] = useState(""),
    [saved, setSaved] = useState(false),
    action = useAction();
  async function save() {
    await action.run(async () => {
      if (!token)
        throw Error("Ссылка восстановления отсутствует. Запросите новую.");
      if (password !== confirmation) throw Error("Пароли не совпадают.");
      if (
        password.length < 10 ||
        !/[\p{L}]/u.test(password) ||
        !/[\p{Nd}]/u.test(password)
      )
        throw Error("Пароль: от 10 символов, минимум буква и цифра.");
      await store.request("/api/auth/reset", { token, password }, "POST");
      await store.refresh();
      setSaved(true);
    });
  }
  return (
    <div className="container page-space reset-page">
      <h1>Новый пароль</h1>
      {saved ? (
        <>
          <p role="status">
            Пароль изменён. Теперь можно войти с новым паролем.
          </p>
          <button className="btn" onClick={() => store.openAuth()}>
            Войти
          </button>
        </>
      ) : (
        <Form
          busy={action.busy}
          error={action.error}
          onSubmit={() => void save()}
        >
          <Field
            label="Новый пароль"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            minLength={10}
            maxLength={128}
            required
          />
          <Field
            label="Повторите пароль"
            type="password"
            autoComplete="new-password"
            value={confirmation}
            onChange={(e) => setConfirmation(e.target.value)}
            required
          />
          <button className="btn" type="submit">
            Сохранить пароль
          </button>
        </Form>
      )}
    </div>
  );
}
