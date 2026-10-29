import { useEffect, useState } from "react";
import type { Order, Selections, CheckoutData } from "./types";
import type { AdminCatalog, Client } from "./admin-types";
import { ShopStore } from "./store";
import {
  Empty,
  Field,
  Form,
  Modal,
  Select,
  useAction,
  useResource,
} from "./ui";
import { Configurator, money } from "./components";
import { OrderSummary, Tracking, placed, closed } from "./orders";
import {
  date,
  fulfillmentLabel,
  paymentLabels,
  paymentStatuses,
  stableKey,
} from "./sessions";
export function AdminOrders({ store }: { store: ShopStore }) {
  const orders = useResource<Order[]>(
      () => store.request("/api/admin/orders"),
      "orders",
    ),
    [tab, setTab] = useState("queue"),
    [query, setQuery] = useState(""),
    [stage, setStage] = useState(""),
    [method, setMethod] = useState(""),
    [payment, setPayment] = useState(""),
    [from, setFrom] = useState(""),
    [to, setTo] = useState(""),
    [page, setPage] = useState(0),
    [selected, setSelected] = useState<string | null>(() =>
      new URLSearchParams(location.search).get("id"),
    ),
    [manual, setManual] = useState(false);
  useEffect(() => {
    const timer = setInterval(() => {
      if (!document.hidden) void orders.reload();
    }, 15000);
    return () => clearInterval(timer);
  }, [store]);
  const rows = (orders.value ?? []).filter((order) => {
    const done = closed(order) || order.fulfillment === "Completed",
      submitted = placed(order),
      day = new Date(
        order.createdUtc.endsWith("Z")
          ? order.createdUtc
          : order.createdUtc + "Z",
      ).toLocaleDateString("en-CA", { timeZone: "Europe/Moscow" });
    return (
      (tab === "queue"
        ? submitted && !done
        : tab === "payments"
          ? !submitted && !done
          : tab === "history"
            ? done
            : order.refunded > 0) &&
      (!stage || order.fulfillment === stage) &&
      (!method || order.checkout.method === method) &&
      (!payment ||
        (payment === "paid") ===
          ["Paid", "PartiallyRefunded", "Refunded"].includes(order.status)) &&
      (!from || day >= from) &&
      (!to || day <= to) &&
      (order.number + " " + order.checkout.name + " " + order.checkout.phone)
        .toLowerCase()
        .includes(query.toLowerCase())
    );
  });
  const pages = Math.max(1, Math.ceil(rows.length / 24)),
    current = Math.min(page, pages - 1);
  return (
    <>
      <div className="admin-page-heading">
        <div>
          <h1>Заказы</h1>
          <p>Принимайте, готовьте и держите гостей в курсе.</p>
        </div>
        <button className="btn" onClick={() => setManual(true)}>
          Новый заказ
        </button>
      </div>
      <div className="admin-tabs">
        {[
          ["queue", "В работе"],
          ["payments", "Ожидают оплаты"],
          ["history", "История"],
          ["refunds", "Возвраты"],
        ].map(([value, label]) => (
          <button
            key={value}
            className={tab === value ? "active" : ""}
            onClick={() => {
              setTab(value!);
              setPage(0);
            }}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="admin-filters">
        <Field
          label="Номер или клиент"
          type="search"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setPage(0);
          }}
        />
        <Select
          label="Статус"
          value={stage}
          onChange={setStage}
          options={[
            ["", "Все статусы"],
            ...[
              "New",
              "Accepted",
              "Preparing",
              "Ready",
              "OutForDelivery",
              "Completed",
              "Rejected",
              "Cancelled",
            ].map(
              (value) =>
                [value, fulfillmentLabel(value, "delivery")] as [
                  string,
                  string,
                ],
            ),
          ]}
        />
        <Select
          label="Получение"
          value={method}
          onChange={setMethod}
          options={[
            ["", "Любое"],
            ["pickup", "Самовывоз"],
            ["delivery", "Доставка"],
          ]}
        />
        <Select
          label="Оплата"
          value={payment}
          onChange={setPayment}
          options={[
            ["", "Любая"],
            ["paid", "Оплачено"],
            ["unpaid", "Не оплачено"],
          ]}
        />
        <Field
          label="С"
          type="date"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
        />
        <Field
          label="По"
          type="date"
          value={to}
          onChange={(e) => setTo(e.target.value)}
        />
        <button
          className="btn btn-outline"
          onClick={() => void orders.reload()}
        >
          Обновить
        </button>
      </div>
      {orders.error && (
        <p className="form-error" role="alert">
          {orders.error}
        </p>
      )}
      <div className="admin-order-grid">
        {rows.slice(current * 24, (current + 1) * 24).map((order) => (
          <article className="admin-panel" key={order.id}>
            <div className="section-heading">
              <h2>{order.number}</h2>
              <span className="admin-badge">
                {fulfillmentLabel(order.fulfillment, order.checkout.method)}
              </span>
            </div>
            <p>
              {order.checkout.name} · {order.checkout.phone}
            </p>
            <p>
              {order.checkout.method === "delivery" ? "Доставка" : "Самовывоз"}{" "}
              · {paymentStatuses[order.status]}
            </p>
            <p className="small muted">
              {date(order.createdUtc)} ·{" "}
              {order.items.reduce((sum, item) => sum + item.quantity, 0)} шт.
            </p>
            <strong>{money(order.total)}</strong>
            <button
              className="btn btn-outline wide"
              onClick={() => setSelected(order.id)}
            >
              Открыть заказ
            </button>
          </article>
        ))}
      </div>
      {!rows.length && orders.value && (
        <Empty title="В этом разделе пока нет заказов" />
      )}
      <div className="admin-pagination">
        <span>
          Найдено: {rows.length} · страница {current + 1} из {pages}
        </span>
        <button
          className="btn btn-outline"
          disabled={!current}
          onClick={() => setPage(current - 1)}
        >
          Назад
        </button>
        <button
          className="btn btn-outline"
          disabled={current + 1 >= pages}
          onClick={() => setPage(current + 1)}
        >
          Далее
        </button>
      </div>
      {selected && (
        <AdminOrderDetail
          key={selected}
          id={selected}
          store={store}
          close={() => setSelected(null)}
          changed={() => void orders.reload()}
        />
      )}
      {manual && (
        <ManualOrder
          store={store}
          close={() => setManual(false)}
          saved={() => {
            setManual(false);
            void orders.reload();
          }}
        />
      )}
    </>
  );
}
function AdminOrderDetail({
  id,
  store,
  close,
  changed,
}: {
  id: string;
  store: ShopStore;
  close: () => void;
  changed: () => void;
}) {
  const detail = useResource<{
      order: Order;
      attempts: { result: string; createdUtc: string }[];
    }>(() => store.request("/api/admin/orders/" + id), id),
    action = useAction(),
    [reason, setReason] = useState(""),
    [refund, setRefund] = useState(false),
    [refundReason, setRefundReason] = useState(""),
    [quantities, setQuantities] = useState<Record<number, number>>({}),
    [delivery, setDelivery] = useState(false),
    [receiptMethod, setReceiptMethod] = useState("cash");
  const order = detail.value?.order;
  useEffect(() => {
    const timer = setInterval(() => {
      if (!document.hidden) void detail.reload();
    }, 10000);
    return () => clearInterval(timer);
  }, [id]);
  async function status(status: string) {
    await action.run(async () => {
      await store.request(
        "/api/admin/orders/" + id + "/status",
        { status, reason },
        "PUT",
      );
      await detail.reload();
      changed();
    });
  }
  async function receipt() {
    await action.run(async () => {
      await store.request(
        "/api/admin/orders/" + id + "/receipt",
        {
          requestKey: stableKey("mh.admin.receipt." + id),
          method: receiptMethod,
        },
        "POST",
      );
      await detail.reload();
      changed();
    });
  }
  async function refundOrder() {
    await action.run(async () => {
      const key = "mh.admin.refund." + id;
      await store.request(
        "/api/admin/orders/" + id + "/refund",
        {
          requestKey: stableKey(key),
          reason: refundReason,
          delivery,
          lines: Object.entries(quantities)
            .filter(([, quantity]) => quantity > 0)
            .map(([index, quantity]) => ({ index: Number(index), quantity })),
        },
        "POST",
      );
      sessionStorage.removeItem(key);
      setRefund(false);
      setQuantities({});
      await detail.reload();
      changed();
    });
  }
  return (
    <>
      <Modal open title={"Заказ " + (order?.number ?? "")} onClose={close} wide>
        {detail.error && <p className="form-error">{detail.error}</p>}
        {order ? (
          <>
            <div className="admin-order-detail">
              <section>
                <Tracking
                  order={order}
                  onRefresh={() => void detail.reload()}
                />
                <p>
                  <strong>{order.checkout.name}</strong> ·{" "}
                  {order.checkout.phone}
                </p>
                <p>
                  {order.checkout.method === "delivery"
                    ? [
                        order.checkout.city,
                        order.checkout.street,
                        order.checkout.apartment,
                      ]
                        .filter(Boolean)
                        .join(", ")
                    : "Самовывоз"}
                </p>
                <p>
                  {order.checkout.timing === "scheduled"
                    ? "Ко времени " +
                      order.checkout.scheduledLocal.replace("T", " ")
                    : "Как можно скорее"}
                </p>
                <p>{order.checkout.comment}</p>
                <p>
                  Источник:{" "}
                  {(
                    {
                      Guest: "Гость",
                      Web: "Сайт",
                      Staff: "Сотрудник",
                      Sample: "Учебный пример",
                    } as Record<string, string>
                  )[order.source] ?? order.source}{" "}
                  · {date(order.createdUtc)}
                </p>
                <p>
                  Оплата: {paymentStatuses[order.status]} ·{" "}
                  {paymentLabels[order.paymentMethod]}
                </p>
                <Field
                  label="Причина изменения или отклонения"
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
                <div className="admin-actions">
                  {order.allowedTransitions.map((next) => (
                    <button
                      key={next}
                      className={
                        next === "Rejected" ? "btn btn-outline" : "btn"
                      }
                      disabled={action.busy}
                      onClick={() => void status(next)}
                    >
                      {fulfillmentLabel(next, order.checkout.method)}
                    </button>
                  ))}
                </div>
                {["cash", "terminal"].includes(order.paymentMethod) &&
                  order.status === "Pending" &&
                  !closed(order) && (
                    <div className="admin-panel">
                      <h3>Оплата при получении</h3>
                      <Select
                        label="Способ поступления"
                        value={receiptMethod}
                        onChange={setReceiptMethod}
                        options={[
                          ["cash", "Наличные"],
                          ["terminal", "Терминал"],
                        ]}
                      />
                      <button
                        className="btn"
                        disabled={action.busy}
                        onClick={() => void receipt()}
                      >
                        Подтвердить получение оплаты
                      </button>
                    </div>
                  )}
                {["Paid", "PartiallyRefunded"].includes(order.status) && (
                  <button
                    className="btn btn-outline"
                    onClick={() => setRefund(true)}
                  >
                    Оформить возврат
                  </button>
                )}
                <h3>История заказа</h3>
                {order.events?.map((event, index) => (
                  <p key={index}>
                    <strong>
                      {fulfillmentLabel(event.status, order.checkout.method)}
                    </strong>{" "}
                    · {date(event.createdUtc)}
                    <br />
                    {event.note}
                  </p>
                ))}
                <h3>Попытки оплаты</h3>
                {detail.value?.attempts.map((attempt, index) => (
                  <p key={index}>
                    {(
                      {
                        success: "Подтверждена",
                        error: "Ошибка",
                        cancel: "Отменена",
                        receipt: "Оплата при получении",
                      } as Record<string, string>
                    )[attempt.result] ?? attempt.result}{" "}
                    · {date(attempt.createdUtc)}
                  </p>
                ))}
                {order.refunds?.map((row) => (
                  <p key={row.id}>
                    Возврат {money(row.amount)} — {row.reason}
                  </p>
                ))}
              </section>
              <aside>
                <OrderSummary store={store} order={order} />
              </aside>
            </div>
            <p className="form-error" role="alert">
              {action.error}
            </p>
          </>
        ) : (
          <p role="status">Загружаем заказ…</p>
        )}
      </Modal>
      <Modal
        open={refund}
        title="Возврат оплаты"
        onClose={() => setRefund(false)}
      >
        <Form
          busy={action.busy}
          error={action.error}
          onSubmit={() => void refundOrder()}
        >
          <Field
            label="Причина возврата"
            value={refundReason}
            required
            maxLength={300}
            onChange={(e) => setRefundReason(e.target.value)}
          />
          {order?.items.map((item, index) => (
            <Field
              key={index}
              label={`${item.name}, количество к возврату`}
              type="number"
              min={0}
              max={Math.max(
                0,
                item.quantity -
                  (order.refunds ?? [])
                    .flatMap((r) => r.lines ?? [])
                    .filter((l) => l.index === index)
                    .reduce((sum, l) => sum + l.quantity, 0),
              )}
              value={quantities[index] ?? 0}
              onChange={(e) =>
                setQuantities((previous) => ({
                  ...previous,
                  [index]: Number(e.target.value),
                }))
              }
            />
          ))}
          <label className="choice">
            <input
              type="checkbox"
              disabled={
                (order?.delivery ?? 0) <=
                (order?.refunds ?? []).reduce(
                  (sum, r) => sum + (r.delivery ?? 0),
                  0,
                )
              }
              checked={delivery}
              onChange={(e) => setDelivery(e.target.checked)}
            />
            Вернуть стоимость доставки
          </label>
          <p className="small muted">
            Сервер проверит уже возвращённые позиции и оставшуюся сумму.
          </p>
          <button className="btn" type="submit">
            Оформить возврат
          </button>
        </Form>
      </Modal>
    </>
  );
}
function ManualOrder({
  store,
  close,
  saved,
}: {
  store: ShopStore;
  close: () => void;
  saved: () => void;
}) {
  const catalog = useResource<AdminCatalog>(
      () => store.request("/api/admin/catalog"),
      "manual-catalog",
    ),
    clients = useResource<Client[]>(
      () => store.request("/api/admin/clients"),
      "manual-clients",
    ),
    [customer, setCustomer] = useState(""),
    [productId, setProductId] = useState(""),
    [lines, setLines] = useState<
      {
        key: string;
        productId: string;
        quantity: number;
        selections: Selections;
      }[]
    >([]),
    [checkout, setCheckout] = useState<CheckoutData>({
      requestKey: crypto.randomUUID(),
      method: "pickup",
      city: "Москва",
      street: "",
      apartment: "",
      name: "",
      phone: "",
      cafe: "demo-house",
      timing: "asap",
      scheduledLocal: "",
      comment: "",
      paymentMethod: "cash",
      consent: true,
    }),
    action = useAction();
  const available = (catalog.value?.products ?? []).filter(
    (p) => !p.archived && !p.draft && p.available,
  );
  const patch = <K extends keyof CheckoutData>(
    key: K,
    value: CheckoutData[K],
  ) => setCheckout((previous) => ({ ...previous, [key]: value }));
  return (
    <Modal open title="Новый заказ" onClose={close} wide>
      <Form
        busy={action.busy}
        error={action.error}
        onSubmit={() =>
          void action.run(async () => {
            await store.request(
              "/api/admin/orders",
              {
                customerId: customer || null,
                checkout,
                lines: lines.map(({ productId, quantity, selections }) => ({
                  productId,
                  quantity,
                  selections,
                })),
              },
              "POST",
            );
            saved();
          })
        }
      >
        <Select
          label="Клиент"
          value={customer}
          onChange={(value) => {
            setCustomer(value);
            const client = clients.value?.find((c) => c.id === value);
            if (client) {
              patch("name", client.name);
              patch(
                "phone",
                client.contact.includes("@") ? "" : client.contact,
              );
            }
          }}
          options={[
            ["", "Заказ без привязки к клиенту"],
            ...(clients.value ?? [])
              .filter((c) => !c.blocked)
              .map(
                (c) => [c.id, c.name + " · " + c.contact] as [string, string],
              ),
          ]}
        />
        <Field
          label="Имя"
          value={checkout.name}
          required
          onChange={(e) => patch("name", e.target.value)}
        />
        <Field
          label="Телефон"
          type="tel"
          value={checkout.phone}
          required
          onChange={(e) => patch("phone", e.target.value)}
        />
        <Select
          label="Получение"
          value={checkout.method}
          onChange={(value) => patch("method", value as "pickup" | "delivery")}
          options={[
            ["pickup", "Самовывоз"],
            ["delivery", "Доставка"],
          ]}
        />
        {checkout.method === "delivery" && (
          <>
            <Field
              label="Город"
              value={checkout.city}
              required
              onChange={(e) => patch("city", e.target.value)}
            />
            <Field
              label="Улица и дом"
              value={checkout.street}
              required
              onChange={(e) => patch("street", e.target.value)}
            />
            <Field
              label="Квартира"
              value={checkout.apartment}
              onChange={(e) => patch("apartment", e.target.value)}
            />
          </>
        )}
        <Select
          label="Время"
          value={checkout.timing}
          onChange={(value) => patch("timing", value as "asap" | "scheduled")}
          options={[
            ["asap", "Как можно скорее"],
            ["scheduled", "Ко времени (МСК)"],
          ]}
        />
        {checkout.timing === "scheduled" && (
          <Field
            label="Дата и время (МСК)"
            type="datetime-local"
            required
            value={checkout.scheduledLocal}
            onChange={(e) =>
              patch("scheduledLocal", e.target.value.slice(0, 16))
            }
          />
        )}
        <Field
          label="Комментарий"
          value={checkout.comment}
          onChange={(e) => patch("comment", e.target.value)}
        />
        <Select
          label="Оплата"
          value={checkout.paymentMethod}
          onChange={(value) => patch("paymentMethod", value)}
          options={Object.entries(paymentLabels)}
        />
        <Select
          label="Добавить товар"
          value={productId}
          onChange={setProductId}
          options={[
            ["", "Выберите товар"],
            ...available.map(
              (p) =>
                [p.id, p.name + " · " + money(p.price)] as [string, string],
            ),
          ]}
        />
        <button
          type="button"
          className="btn btn-outline"
          disabled={!productId}
          onClick={() => {
            const p = available.find((p) => p.id === productId);
            if (p)
              setLines([
                ...lines,
                {
                  key: crypto.randomUUID(),
                  productId: p.id,
                  quantity: 1,
                  selections: store.defaults(p),
                },
              ]);
          }}
        >
          Добавить позицию
        </button>
        {lines.map((line, index) => {
          const p = available.find((p) => p.id === line.productId)!;
          return (
            <section className="admin-panel" key={line.key}>
              <h3>{p.name}</h3>
              <Field
                label="Количество"
                type="number"
                min={1}
                max={99}
                value={line.quantity}
                onChange={(e) =>
                  setLines((previous) =>
                    previous.map((value, i) =>
                      i === index
                        ? { ...value, quantity: Number(e.target.value) }
                        : value,
                    ),
                  )
                }
              />
              <div className="configurator">
                <Configurator
                  product={p}
                  initial={line.selections}
                  onInvalid={store.toast}
                  onChange={(selections) =>
                    setLines((previous) =>
                      previous.map((value, i) =>
                        i === index ? { ...value, selections } : value,
                      ),
                    )
                  }
                />
              </div>
              <strong>
                {money(
                  store.calculated(p, line.selections).price * line.quantity,
                )}
              </strong>
              <button
                type="button"
                className="danger-link"
                onClick={() => setLines(lines.filter((_, i) => i !== index))}
              >
                Удалить позицию
              </button>
            </section>
          );
        })}
        <button className="btn" type="submit" disabled={!lines.length}>
          Создать заказ
        </button>
      </Form>
    </Modal>
  );
}
