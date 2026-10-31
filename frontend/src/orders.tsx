import { useEffect, useState } from "react";
import type { Order } from "./types";
import { ShopStore, useShop } from "./store";
import { Field, Modal, Empty, useAction, useResource } from "./ui";
import { Totals } from "./cart-auth";
import { money } from "./components";
import { image } from "./media";
import {
  date,
  forgetOrder,
  orderUrl,
  paymentLabels,
  paymentStatuses,
  rememberOrder,
  stableKey,
  fulfillmentLabel,
} from "./sessions";
import { orderStatus } from "./order-state";
export const placed = (order: Order) =>
  ["Paid", "PartiallyRefunded", "Refunded"].includes(order.status) ||
  ["cash", "terminal"].includes(order.paymentMethod);
export const closed = (order: Order) =>
  ["Cancelled", "Rejected"].includes(order.fulfillment);
export function OrderSummary({
  order,
  store,
}: {
  order: Order;
  store: ShopStore;
}) {
  return (
    <section className="payment-summary">
      <h2>Ваш заказ</h2>
      {order.items.map((item, index) => (
        <div className="summary-line" key={index}>
          <img src={image(item.image)} alt={item.name} width="58" height="60" />
          <div>
            <h3>{item.name}</h3>
            <p>{item.summary.join(", ") || item.portion}</p>
            <span>{item.quantity} шт.</span>
          </div>
          <strong>{money(item.total)}</strong>
        </div>
      ))}
      <Totals subtotal={order.subtotal} delivery={order.delivery} />
      <p className="small muted">
        {order.checkout.method === "delivery"
          ? "Доставка: " +
            [
              order.checkout.city,
              order.checkout.street,
              order.checkout.apartment,
            ]
              .filter(Boolean)
              .join(", ")
          : "Самовывоз: " + store.app.state?.settings.cafeName}
        <br />
        {order.checkout.timing === "scheduled"
          ? "Ко времени: " +
            order.checkout.scheduledLocal.replace("T", " ") +
            " (МСК)"
          : "Как можно скорее"}
      </p>
    </section>
  );
}
export function Tracking({
  order,
  onRefresh,
}: {
  order: Order;
  onRefresh: () => void;
}) {
  const info = orderStatus(order);
  return (
    <section
      className="order-tracking"
      aria-label="Статус заказа"
      aria-live="polite"
      data-order-stage={order.fulfillment}
    >
      <h2>{info.title}</h2>
      <p>{info.note}</p>
      {!!info.steps.length && (
        <ol className="order-progress">
          {info.steps.map((step, index) => (
            <li
              key={index}
              className={step.state}
              aria-current={step.state === "current" ? "step" : undefined}
            >
              {step.state === "complete" ? "✓ " : ""}
              {step.label}
            </li>
          ))}
        </ol>
      )}
      <p className="order-update">
        Статус обновляется автоматически каждые 10 секунд.
      </p>
      <button type="button" className="text-button" onClick={onRefresh}>
        Обновить статус
      </button>
    </section>
  );
}
export function OrderPage({
  store,
  payment = false,
}: {
  store: ShopStore;
  payment?: boolean;
}) {
  useShop(store);
  const id = location.pathname.split("/").filter(Boolean).at(-1)!;
  const resource = useResource<Order>(async () => {
    const token = new URLSearchParams(location.hash.slice(1)).get("access");
    if (token) {
      try {
        await store.request("/api/orders/" + id + "/access", { token }, "POST");
        rememberOrder({ id, guestAccessToken: token });
      } catch (error) {
        if ((error as { status?: number }).status !== 404) throw error;
        await store.request("/api/orders/" + id);
        forgetOrder(id);
      }
    }
    return store.request<Order>("/api/orders/" + id);
  }, id);
  const action = useAction(),
    [cancel, setCancel] = useState(false),
    [link, setLink] = useState("");
  useEffect(() => {
    const timer = setInterval(() => {
      if (!document.hidden) void resource.reload();
    }, 10000);
    const visible = () => {
      if (!document.hidden) void resource.reload();
    };
    document.addEventListener("visibilitychange", visible);
    return () => {
      clearInterval(timer);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [id]);
  const order = resource.value;
  useEffect(() => {
    if (payment && order && (placed(order) || closed(order)))
      location.replace(orderUrl(order.id));
  }, [payment, order]);
  async function pay(result: string) {
    await action.run(async () => {
      const key = "mh.pay." + id + "." + result,
        updated = await store.request<Order>(
          "/api/orders/" + id + "/payment",
          { attemptKey: stableKey(key), result },
          "POST",
        );
      sessionStorage.removeItem(key);
      await store.refresh();
      location.href = orderUrl(updated.id);
    });
  }
  async function cancelOrder() {
    await action.run(async () => {
      const key = "mh.cancel." + id;
      await store.request(
        "/api/orders/" + id + "/cancel",
        { requestKey: stableKey(key) },
        "POST",
      );
      sessionStorage.removeItem(key);
      setCancel(false);
      location.href = orderUrl(id);
    });
  }
  async function claim() {
    const save = async () => {
      await action.run(async () => {
        await store.request("/api/orders/" + id + "/claim", {}, "POST");
        forgetOrder(id);
        location.href = "/Account";
      });
    };
    if (store.app.state?.authenticated) await save();
    else store.openAuth(() => void save());
  }
  async function copy() {
    await action.run(async () => {
      const result = await store.request<{
        id: string;
        guestAccessToken: string;
      }>("/api/orders/" + id + "/access-link", {}, "POST");
      rememberOrder(result);
      const value = new URL(orderUrl(id), location.origin).href;
      setLink(value);
      try {
        await navigator.clipboard.writeText(value);
        store.toast("Личная ссылка скопирована");
      } catch {
        store.toast("Скопируйте ссылку из поля ниже");
      }
    });
  }
  if (resource.error)
    return (
      <Empty title="Не удалось открыть заказ" text={resource.error}>
        <button
          className="btn"
          onClick={() => store.openAuth(() => void resource.reload())}
        >
          Войти
        </button>
        <button
          className="btn btn-outline"
          onClick={() => void resource.reload()}
        >
          Попробовать снова
        </button>
        <a href="/Menu">Открыть меню</a>
      </Empty>
    );
  if (!order)
    return (
      <p className="container page-space" role="status">
        Загружаем заказ…
      </p>
    );
  const guest = order.guest && (
    <section className="guest-order-panel">
      <h2>Заказ без регистрации</h2>
      <p>
        Статус доступен в этом браузере. Личная ссылка позволяет открыть заказ
        на другом устройстве в течение 30 дней и даёт доступ к данным этого
        заказа.
      </p>
      <div className="guide-actions">
        <button
          type="button"
          className="btn btn-outline"
          disabled={action.busy}
          onClick={() => void copy()}
        >
          Скопировать личную ссылку
        </button>
        {placed(order) && (
          <button
            type="button"
            className="btn"
            disabled={action.busy}
            onClick={() => void claim()}
          >
            {store.app.state?.authenticated
              ? "Сохранить в моём профиле"
              : "Войти и сохранить заказ"}
          </button>
        )}
      </div>
      {link && (
        <Field label="Личная ссылка на этот заказ" readOnly value={link} />
      )}
    </section>
  );
  const cancellation = order.canCancel && (
    <section className="order-cancel">
      <button
        type="button"
        className="btn btn-outline"
        onClick={() => setCancel(true)}
      >
        Отменить заказ
      </button>
      <p>Можно отменить, пока заказ не выдан или не доставлен.</p>
    </section>
  );
  const summary = <OrderSummary order={order} store={store} />;
  return (
    <div className="container page-space">
      {payment ? (
        <div className="payment-layout">
          <section className="payment-left">
            <p className="small muted">Заказ {order.number}</p>
            <h1>
              {order.paymentMethod === "sbp"
                ? "Оплата через СБП"
                : order.paymentMethod === "yoomoney"
                  ? "Оплата ЮMoney"
                  : "Удобная оплата"}
            </h1>
            <Tracking order={order} onRefresh={() => void resource.reload()} />
            {cancellation}
            {guest}
            {summary}
            <a className="text-link" href="/Checkout">
              Изменить оформление
            </a>
          </section>
          <section className="payment-card">
            <h2>{paymentLabels[order.paymentMethod]}</h2>
            {order.paymentMethod === "sbp" ? (
              <div className="sbp-qr">
                <img
                  src={"/api/orders/" + id + "/qr"}
                  alt="QR-код заказа"
                  width="270"
                  height="270"
                />
              </div>
            ) : order.paymentMethod === "yoomoney" ? (
              <div className="wallet-visual">
                <span>Ю</span>
                <strong>Кошелёк ЮMoney</strong>
              </div>
            ) : (
              <div className="demo-bank-card">
                <span className="card-chip" />
                <div>
                  <strong>Mellow House</strong>
                  <p className="card-dots">•••• 4242</p>
                </div>
              </div>
            )}
            <button
              className="btn wide"
              disabled={action.busy}
              onClick={() => void pay("success")}
            >
              Подтвердить тестовую оплату · {money(order.total)}
            </button>
            <div className="payment-tests">
              <button disabled={action.busy} onClick={() => void pay("error")}>
                Имитировать ошибку
              </button>
              <button disabled={action.busy} onClick={() => void pay("cancel")}>
                Отменить оплату
              </button>
            </div>
            <details className="other-payment">
              <summary>Другой способ оплаты</summary>
              {(store.app.state?.settings.paymentMethods ?? [])
                .filter((method) => method !== order.paymentMethod)
                .map((method) => (
                  <button
                    key={method}
                    type="button"
                    className="text-button wide"
                    disabled={action.busy}
                    onClick={() =>
                      void action.run(async () => {
                        await store.request(
                          "/api/orders/" + id + "/method",
                          { method },
                          "PUT",
                        );
                        location.href = orderUrl(id, "Payment");
                      })
                    }
                  >
                    {paymentLabels[method]}
                  </button>
                ))}
            </details>
          </section>
        </div>
      ) : (
        <div className="result-layout">
          <section>
            <h1>Заказ {order.number}</h1>
            <Tracking order={order} onRefresh={() => void resource.reload()} />
            <div className="result-actions">
              {!placed(order) && !closed(order) && (
                <a className="btn" href={orderUrl(id, "Payment")}>
                  Перейти к оплате
                </a>
              )}
              <a className="btn btn-outline" href="/Menu">
                Вернуться в меню
              </a>
              {!order.guest && (
                <a className="btn btn-outline" href="/Account">
                  История заказов
                </a>
              )}
            </div>
            {cancellation}
            {guest}
            <section className="customer-order-events">
              <h2>История заказа</h2>
              {order.events?.map((event, index) => (
                <p key={index}>
                  <strong>
                    {fulfillmentLabel(event.status, order.checkout.method)}
                  </strong>{" "}
                  <small>{date(event.createdUtc)}</small>
                  <br />
                  {event.note}
                </p>
              ))}
            </section>
          </section>
          <aside>
            {summary}
            <div className="notice">
              Оплата: {paymentStatuses[order.status] ?? order.status}
              {order.refunded > 0 && (
                <>
                  <br />
                  Возврат: {money(order.refunded)}
                </>
              )}
            </div>
            {order.refunds?.map((refund) => (
              <p className="notice" key={refund.id}>
                Возврат {money(refund.amount)}. {refund.reason}
              </p>
            ))}
          </aside>
        </div>
      )}
      {action.error && (
        <p className="form-error" role="alert">
          {action.error}
        </p>
      )}
      <Modal
        open={cancel}
        title={"Отменить заказ " + order.number + "?"}
        onClose={() => setCancel(false)}
      >
        <p>
          Кафе перестанет готовить и выдавать этот заказ.{" "}
          {["Paid", "PartiallyRefunded"].includes(order.status)
            ? `Возврат остатка: ${money(order.total - order.refunded)}. Сейчас оплата тестовая: реального списания и банковского возврата нет.`
            : "Оплачивать отменённый заказ не нужно."}
        </p>
        <p className="form-error" role="alert">
          {action.error}
        </p>
        <div className="dialog-actions">
          <button className="btn btn-outline" onClick={() => setCancel(false)}>
            Оставить заказ
          </button>
          <button
            className="btn"
            disabled={action.busy}
            onClick={() => void cancelOrder()}
          >
            Отменить заказ
          </button>
        </div>
      </Modal>
    </div>
  );
}
