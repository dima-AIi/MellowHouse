import { useEffect, useRef, useState } from "react";
import { ApiError, ShopStore, useShop } from "./store";
import type { CheckoutData, Order } from "./types";
import { Field, Empty, Form, useAction } from "./ui";
import { Method, Totals } from "./cart-auth";
import { money } from "./components";
import { image } from "./media";
import { orderUrl, rememberOrder, paymentLabels, validPhone } from "./sessions";
const moscow = (date: Date) =>
  new Intl.DateTimeFormat("sv-SE", {
    timeZone: "Europe/Moscow",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  })
    .format(date)
    .replace(" ", "T");
export function Checkout({ store }: { store: ShopStore }) {
  useShop(store);
  const state = store.app.state!,
    settings = state.settings,
    cart = store.cartView(),
    action = useAction();
  const [data, setData] = useState<CheckoutData>({
      requestKey: crypto.randomUUID(),
      method: store.app.method,
      city: "",
      street: "",
      apartment: "",
      name: state.profile?.name ?? "",
      phone: state.profile?.deliveryPhone ?? "",
      cafe: "",
      timing: "asap",
      scheduledLocal: "",
      comment: "",
      paymentMethod: settings.paymentMethods?.[0] ?? "card",
      consent: false,
    }),
    [loaded, setLoaded] = useState(false),
    [saved, setSaved] = useState("");
  const timeGroup = useRef<HTMLFieldSetElement>(null);
  const timeError =
    action.field === "timing" || action.field === "scheduledLocal"
      ? action.error
      : "";
  useEffect(() => {
    if (timeError) {
      timeGroup.current?.scrollIntoView({
        block: "center",
        behavior: matchMedia("(prefers-reduced-motion:reduce)").matches
          ? "instant"
          : "smooth",
      });
      timeGroup.current?.focus({ preventScroll: true });
    }
  }, [timeError]);
  const dirty = useRef(false),
    current = useRef(data);
  current.current = data;
  useEffect(() => {
    let active = true;
    store
      .request<Partial<CheckoutData>>("/api/checkout-draft")
      .then((draft) => {
        if (active) {
          setData((previous) => ({
            ...previous,
            ...draft,
            method: store.app.method,
            name: draft.name || previous.name,
            phone: draft.phone || previous.phone,
          }));
          setLoaded(true);
        }
      })
      .catch((error) => {
        if (active) {
          action.setError(error.message);
          setLoaded(true);
        }
      });
    return () => {
      active = false;
    };
  }, [store]);
  useEffect(() => {
    if (!loaded || !dirty.current || action.busy) return;
    setSaved("Сохраняем…");
    const timer = setTimeout(() => {
      void store
        .request(
          "/api/checkout-draft",
          { ...data, method: store.app.method },
          "PUT",
        )
        .then(() => setSaved("Данные оформления сохранены"))
        .catch((error) => setSaved(error.message));
    }, 650);
    return () => clearTimeout(timer);
  }, [data, loaded, store, store.app.method, action.busy]);
  function patch<K extends keyof CheckoutData>(key: K, value: CheckoutData[K]) {
    dirty.current = true;
    if (
      key === action.field ||
      (key === "timing" && action.field === "scheduledLocal")
    )
      action.setError("");
    setData((previous) => ({ ...previous, [key]: value }));
  }
  async function place() {
    await action.run(async () => {
      if (!validPhone(data.phone))
        throw new ApiError("Введите телефон: +7 и десять цифр.", 400, "phone");
      const body = {
        ...current.current,
        scheduledLocal: current.current.scheduledLocal.slice(0, 16),
        method: store.app.method,
      };
      const signature = JSON.stringify({
        ...body,
        signature: undefined,
        requestKey: null,
        revision: cart.revision,
      });
      if (body.signature && body.signature !== signature)
        body.requestKey = crypto.randomUUID();
      body.signature = signature;
      setData(body);
      await store.request("/api/checkout-draft", body, "PUT");
      const order = await store.request<Order>("/api/orders", body, "POST");
      rememberOrder(order);
      location.href = orderUrl(
        order.id,
        order.status === "Paid" ||
          ["cash", "terminal"].includes(order.paymentMethod)
          ? "Order"
          : "Payment",
      );
    });
  }
  async function login() {
    await action.run(async () => {
      const saved = { ...data, method: store.app.method };
      await store.request("/api/checkout-draft", saved, "PUT");
      store.openAuth(() => {
        void store
          .request(
            "/api/checkout-draft",
            { ...saved, requestKey: crypto.randomUUID(), signature: null },
            "PUT",
          )
          .then(() => location.reload())
          .catch((error) => store.toast(error.message));
      });
    });
  }
  if (!cart.count)
    return (
      <Empty
        title="В корзине пока пусто"
        text="Сначала выберите любимые блюда."
      >
        <a className="btn" href="/Menu">
          Открыть меню
        </a>
      </Empty>
    );
  const fee =
    store.app.method === "delivery" ? (state.deliveryPrice ?? 190) : 0;
  return (
    <div className="container page-space">
      <h1>Оформление заказа</h1>
      {!state.authenticated && (
        <p className="notice">
          Можно оформить без регистрации.{" "}
          <button
            className="text-button"
            type="button"
            onClick={() => void login()}
          >
            Войти в профиль
          </button>
        </p>
      )}
      <Form
        busy={action.busy || !loaded}
        error={
          timeError ||
          ["city", "phone", "name", "street"].includes(action.field ?? "")
            ? undefined
            : action.error
        }
        onSubmit={() => void place()}
      >
        <div className="checkout-layout">
          <div className="checkout-fields">
            <section className="checkout-panel">
              <Method store={store} />
              {store.app.method === "delivery" ? (
                <>
                  <Field
                    label="Город"
                    name="city"
                    value={data.city}
                    required
                    maxLength={80}
                    error={action.field === "city" ? action.error : undefined}
                    onChange={(e) => patch("city", e.target.value)}
                  />
                  <small>
                    Доставка: {settings.deliveryCities?.join(", ") ?? "Москва"}.
                    За пределами этих городов доступен самовывоз.
                  </small>
                  <Field
                    label="Улица и дом"
                    name="street"
                    error={action.field === "street" ? action.error : undefined}
                    value={data.street}
                    required
                    maxLength={160}
                    onChange={(e) => patch("street", e.target.value)}
                  />
                  <Field
                    label="Квартира"
                    name="apartment"
                    value={data.apartment}
                    maxLength={30}
                    onChange={(e) => patch("apartment", e.target.value)}
                  />
                </>
              ) : (
                <>
                  <div className="field">
                    <label htmlFor="cafe">Выберите кафе</label>
                    <select
                      id="cafe"
                      value={data.cafe}
                      onChange={(e) => patch("cafe", e.target.value)}
                      required
                    >
                      <option value="">Выберите кафе</option>
                      <option value="demo-house">
                        {settings.cafeName ?? "Mellow House"}
                      </option>
                    </select>
                  </div>
                  <p className="small muted">
                    {settings.address} · {settings.hours} · Самовывоз —
                    бесплатно.
                  </p>
                </>
              )}
              <Field
                label="Имя"
                name="name"
                error={action.field === "name" ? action.error : undefined}
                autoComplete="given-name"
                maxLength={80}
                value={data.name}
                required
                onChange={(e) => patch("name", e.target.value)}
              />
              <Field
                label="Телефон для связи по заказу"
                name="phone"
                error={action.field === "phone" ? action.error : undefined}
                type="tel"
                autoComplete="tel"
                value={data.phone}
                required
                onChange={(e) => patch("phone", e.target.value)}
              />
              <fieldset
                className="time-choices"
                ref={timeGroup}
                tabIndex={-1}
                aria-invalid={timeError ? true : undefined}
                aria-describedby={timeError ? "timing-error" : undefined}
              >
                <legend>Время получения</legend>
                {(["asap", "scheduled"] as const).map((timing) => (
                  <label className="choice" key={timing}>
                    <input
                      type="radio"
                      name="timing"
                      value={timing}
                      checked={data.timing === timing}
                      onChange={() => patch("timing", timing)}
                    />
                    {timing === "asap" ? "Как можно скорее" : "Ко времени"}
                  </label>
                ))}
                {data.timing === "scheduled" && (
                  <>
                    <Field
                      label="Дата и время (МСК)"
                      name="scheduledLocal"
                      type="datetime-local"
                      value={data.scheduledLocal}
                      min={moscow(new Date(Date.now() + 30 * 60000))}
                      max={moscow(new Date(Date.now() + 7 * 86400000))}
                      required
                      onChange={(e) =>
                        patch("scheduledLocal", e.target.value.slice(0, 16))
                      }
                    />
                    <small>
                      От 30 минут до 7 дней вперёд, в рабочие дни{" "}
                      {settings.openingTime ?? "08:00"}–
                      {settings.closingTime ?? "22:00"} (МСК).
                    </small>
                  </>
                )}
                {timeError && (
                  <p id="timing-error" className="form-error" role="alert">
                    {timeError}
                  </p>
                )}
              </fieldset>
              <div className="field">
                <label htmlFor="checkout-comment">
                  Комментарий к заказу (необязательно)
                </label>
                <textarea
                  id="checkout-comment"
                  name="comment"
                  maxLength={600}
                  value={data.comment}
                  onChange={(e) => patch("comment", e.target.value)}
                  placeholder={
                    store.app.method === "delivery"
                      ? "Домофон, ориентир, как удобнее передать заказ…"
                      : "Пожелания к приготовлению и упаковке…"
                  }
                />
              </div>
            </section>
            <section className="checkout-panel">
              <h2>Способ оплаты</h2>
              {(settings.paymentMethods ?? ["card"]).map((method) => (
                <label key={method} className="choice">
                  <input
                    type="radio"
                    name="paymentMethod"
                    value={method}
                    checked={data.paymentMethod === method}
                    onChange={() => patch("paymentMethod", method)}
                  />
                  {paymentLabels[method] ?? method}
                </label>
              ))}
            </section>
            <p className="small muted" role="status">
              {saved}
            </p>
          </div>
          <aside className="checkout-summary">
            <h2>Ваш заказ</h2>
            {cart.items.map((line) => (
              <div className="summary-line" key={line.id}>
                <img
                  src={image(line.product.image)}
                  alt={line.product.name}
                  width="58"
                  height="60"
                />
                <div>
                  <h3>{line.product.name}</h3>
                  <p>{line.summary.join(", ") || line.product.portion}</p>
                  <div className="quantity">
                    <button
                      type="button"
                      aria-label={"Уменьшить количество " + line.product.name}
                      onClick={() =>
                        void action.run(() =>
                          line.quantity === 1
                            ? store.remove(line.id)
                            : store.edit(
                                line.id,
                                line.selections,
                                line.quantity - 1,
                              ),
                        )
                      }
                    >
                      −
                    </button>
                    <output>{line.quantity}</output>
                    <button
                      type="button"
                      aria-label={"Увеличить количество " + line.product.name}
                      onClick={() =>
                        void action.run(() =>
                          store.edit(
                            line.id,
                            line.selections,
                            line.quantity + 1,
                          ),
                        )
                      }
                    >
                      +
                    </button>
                  </div>
                </div>
                <strong>{money(line.total)}</strong>
              </div>
            ))}
            <Totals subtotal={cart.subtotal} delivery={fee} />
            {cart.items.some(
              (line) => line.available === false || !line.product.available,
            ) && (
              <p className="form-error">
                В корзине есть недоступные товары. Удалите их, чтобы продолжить.
              </p>
            )}
            <label className="choice">
              <input
                type="checkbox"
                checked={data.consent}
                required
                onChange={(e) => patch("consent", e.target.checked)}
              />
              Принимаю <a href="/Legal">условия оформления заказа</a>.
            </label>
            <button
              className="btn wide"
              type="submit"
              disabled={cart.items.some(
                (line) => line.available === false || !line.product.available,
              )}
            >
              {["cash", "terminal"].includes(data.paymentMethod)
                ? "Оформить заказ"
                : "Перейти к оплате"}{" "}
              · {money(cart.subtotal + fee)}
            </button>
          </aside>
        </div>
      </Form>
    </div>
  );
}
