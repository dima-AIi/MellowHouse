import { useState } from "react";
import { ShopStore } from "./store";
import type {
  AdminSettings,
  AuditRow,
  Client,
  ClientDetail,
} from "./admin-types";
import type { PublicSettings } from "./types";
import {
  Empty,
  Field,
  Form,
  Modal,
  Select,
  useAction,
  useResource,
  useUnsavedChanges,
} from "./ui";
import { date, fulfillmentLabel, paymentLabels } from "./sessions";
import { money } from "./components";
export function AdminClients({ store }: { store: ShopStore }) {
  const clients = useResource<Client[]>(
      () => store.request("/api/admin/clients"),
      "clients",
    ),
    [query, setQuery] = useState(""),
    [filter, setFilter] = useState("all"),
    [page, setPage] = useState(0),
    [selected, setSelected] = useState<string | null>(null);
  const rows = (clients.value ?? []).filter(
      (c) =>
        (c.name + " " + c.contact)
          .toLowerCase()
          .includes(query.toLowerCase()) &&
        (filter === "all" ||
          (filter === "team" && c.role === "Admin") ||
          (filter === "blocked" && c.blocked) ||
          (filter === "active" && c.active) ||
          (filter === "sample" && c.sample)),
    ),
    pages = Math.max(1, Math.ceil(rows.length / 24)),
    current = Math.min(page, pages - 1);
  return (
    <>
      <div className="admin-page-heading">
        <div>
          <h1>Гости и команда</h1>
          <p>Профили, история заказов и управление доступом.</p>
        </div>
      </div>
      <div className="admin-filters">
        <Field
          label="Имя, телефон или почта"
          type="search"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setPage(0);
          }}
        />
        <Select
          label="Показать"
          value={filter}
          onChange={(value) => {
            setFilter(value);
            setPage(0);
          }}
          options={[
            ["all", "Все профили"],
            ["team", "Команда"],
            ["active", "Сейчас на сайте"],
            ["blocked", "Заблокированные"],
            ["sample", "Учебные профили"],
          ]}
        />
        <button
          className="btn btn-outline"
          onClick={() => void clients.reload()}
        >
          Обновить
        </button>
      </div>
      {clients.error && <p className="form-error">{clients.error}</p>}
      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Гость</th>
              <th>Контакт</th>
              <th>Доступ</th>
              <th>Заказы</th>
              <th>Последняя активность</th>
              <th>Действия</th>
            </tr>
          </thead>
          <tbody>
            {rows.slice(current * 24, (current + 1) * 24).map((c) => (
              <tr key={c.id}>
                <td>
                  {c.name || "Без имени"}
                  {c.sample && <small> · Учебный профиль</small>}
                </td>
                <td>{c.contact}</td>
                <td>
                  {c.blocked
                    ? "Заблокирован"
                    : c.role === "Admin"
                      ? "Команда"
                      : "Клиент"}
                </td>
                <td>{c.orders ?? 0}</td>
                <td>
                  {c.lastActiveUtc ? date(c.lastActiveUtc) : "Нет данных"}
                </td>
                <td>
                  <button
                    className="text-button"
                    onClick={() => setSelected(c.id)}
                  >
                    Открыть профиль
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="admin-pagination">
        <span>Найдено: {rows.length}</span>
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
        <ClientModal
          id={selected}
          store={store}
          close={() => setSelected(null)}
          changed={() => void clients.reload()}
        />
      )}
    </>
  );
}
function ClientModal({
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
  const detail = useResource<ClientDetail>(
      () => store.request("/api/admin/clients/" + id),
      id,
    ),
    action = useAction(),
    [reason, setReason] = useState(""),
    [reset, setReset] = useState(""),
    [confirm, setConfirm] = useState(false),
    c = detail.value?.profile;
  return (
    <>
      <Modal open title={c?.name || "Профиль клиента"} onClose={close} wide>
        {c ? (
          <>
            <p>
              {c.contact} · {c.role === "Admin" ? "Команда" : "Клиент"} ·{" "}
              {c.blocked ? "Заблокирован" : "Активен"}
            </p>
            <p>
              Создан: {date(c.createdUtc)} · Последний вход:{" "}
              {c.lastLoginUtc ? date(c.lastLoginUtc) : "Нет данных"}
            </p>
            {c.blockReason && <p className="notice">{c.blockReason}</p>}
            <Field
              label="Причина блокировки"
              value={reason}
              maxLength={300}
              onChange={(e) => setReason(e.target.value)}
            />
            <button
              className="btn btn-outline"
              disabled={action.busy}
              onClick={() => setConfirm(true)}
            >
              {c.blocked ? "Разблокировать" : "Заблокировать"}
            </button>
            {c.hasPassword && !c.sample && (
              <button
                className="text-button"
                disabled={action.busy}
                onClick={() =>
                  void action.run(async () => {
                    const result = await store.request<{ url: string }>(
                      "/api/admin/clients/" + id + "/reset",
                      {},
                      "POST",
                    );
                    setReset(result.url);
                  })
                }
              >
                Подготовить восстановление пароля
              </button>
            )}
            {reset && (
              <Field
                label="Личная ссылка восстановления"
                value={new URL(reset, location.origin).href}
                readOnly
              />
            )}
            <h3>История заказов</h3>
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Заказ</th>
                    <th>Дата</th>
                    <th>Сумма</th>
                    <th>Статус</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.value?.orders.map((order) => (
                    <tr key={order.id}>
                      <td>
                        <a href={"/Admin/Orders?id=" + order.id}>
                          {order.number}
                        </a>
                      </td>
                      <td>{date(order.createdUtc)}</td>
                      <td>{money(order.total)}</td>
                      <td>
                        {fulfillmentLabel(
                          order.fulfillment,
                          order.checkout.method,
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="form-error" role="alert">
              {action.error}
            </p>
          </>
        ) : (
          <p>{detail.error || "Загружаем профиль…"}</p>
        )}
      </Modal>
      <Modal
        open={confirm}
        title={
          c?.blocked ? "Разблокировать профиль?" : "Заблокировать профиль?"
        }
        onClose={() => setConfirm(false)}
      >
        <p>
          Изменится доступ этого пользователя к сайту. Сервер не позволяет
          заблокировать последнего администратора.
        </p>
        <p className="form-error" role="alert">
          {action.error}
        </p>
        <button
          className="btn"
          disabled={action.busy}
          onClick={() =>
            void action.run(async () => {
              await store.request(
                "/api/admin/clients/" + id + "/block",
                { blocked: !c!.blocked, reason },
                "PUT",
              );
              setConfirm(false);
              await detail.reload();
              changed();
            })
          }
        >
          Подтвердить
        </button>
      </Modal>
    </>
  );
}
export function AdminSettingsPage({ store }: { store: ShopStore }) {
  const resource = useResource<AdminSettings>(
      () => store.request("/api/admin/settings"),
      "settings",
    ),
    [tab, setTab] = useState("settings");
  return (
    <>
      <div className="admin-page-heading">
        <div>
          <h1>Настройки</h1>
          <p>
            Данные кафе, получение заказов, содержимое сайта и журнал действий.
          </p>
        </div>
      </div>
      <div className="admin-tabs">
        <button
          className={tab === "settings" ? "active" : ""}
          onClick={() => setTab("settings")}
        >
          Настройки кафе
        </button>
        <button
          className={tab === "audit" ? "active" : ""}
          onClick={() => setTab("audit")}
        >
          Журнал действий
        </button>
      </div>
      {tab === "audit" ? (
        <Audit store={store} />
      ) : resource.value ? (
        <SettingsForm
          key={JSON.stringify(resource.value.site)}
          initial={resource.value}
          store={store}
        />
      ) : (
        <p role="status">{resource.error || "Загружаем настройки…"}</p>
      )}
    </>
  );
}
function SettingsForm({
  initial,
  store,
}: {
  initial: AdminSettings;
  store: ShopStore;
}) {
  const [data, setData] = useState<PublicSettings>(initial.site),
    [saved, setSaved] = useState(""),
    [snapshot, setSnapshot] = useState(JSON.stringify(initial.site)),
    action = useAction();
  useUnsavedChanges(JSON.stringify(data) !== snapshot);
  const patch = <K extends keyof PublicSettings>(
    key: K,
    value: PublicSettings[K],
  ) => setData((previous) => ({ ...previous, [key]: value }));
  return (
    <Form
      busy={action.busy}
      error={action.error}
      onSubmit={() =>
        void action.run(async () => {
          await store.request("/api/admin/settings", data, "PUT");
          await store.refresh();
          setSaved("Настройки сохранены.");
          setSnapshot(JSON.stringify(data));
        })
      }
    >
      <section className="admin-panel">
        <h2>Кафе и получение</h2>
        <div className="admin-fields">
          {(
            [
              ["name", "Название сайта"],
              ["cafeName", "Кафе для самовывоза"],
              ["phone", "Контактный телефон"],
              ["email", "Контактная почта"],
              ["address", "Адрес"],
            ] as const
          ).map(([key, label]) => (
            <Field
              key={key}
              label={label}
              value={data[key] ?? ""}
              onChange={(e) => patch(key, e.target.value)}
            />
          ))}
          <Field
            label="Доставка, ₽"
            type="number"
            min={0}
            max={10000}
            value={data.deliveryPrice ?? 0}
            onChange={(e) => patch("deliveryPrice", Number(e.target.value))}
          />
          <Field
            label="Открытие (МСК)"
            type="time"
            value={data.openingTime ?? "08:00"}
            required
            onChange={(e) => patch("openingTime", e.target.value)}
          />
          <Field
            label="Закрытие (МСК)"
            type="time"
            value={data.closingTime ?? "22:00"}
            required
            onChange={(e) => patch("closingTime", e.target.value)}
          />
          <ArrayField
            label="Города доставки через запятую"
            value={data.deliveryCities ?? []}
            change={(value) => patch("deliveryCities", value)}
          />
          <ArrayField
            label="Закрытые даты (ГГГГ-ММ-ДД) через запятую"
            value={data.closedDates ?? []}
            change={(value) => patch("closedDates", value)}
          />
        </div>
        <p>Нерабочие дни недели</p>
        {[
          "Воскресенье",
          "Понедельник",
          "Вторник",
          "Среда",
          "Четверг",
          "Пятница",
          "Суббота",
        ].map((label, index) => (
          <label className="choice" key={label}>
            <input
              type="checkbox"
              checked={data.closedWeekdays?.includes(index) ?? false}
              onChange={(e) =>
                patch(
                  "closedWeekdays",
                  e.target.checked
                    ? [...(data.closedWeekdays ?? []), index]
                    : (data.closedWeekdays ?? []).filter(
                        (value) => value !== index,
                      ),
                )
              }
            />
            {label}
          </label>
        ))}
      </section>
      <section className="admin-panel">
        <h2>Оплата</h2>
        {Object.entries(paymentLabels).map(([id, label]) => (
          <label className="choice" key={id}>
            <input
              type="checkbox"
              checked={data.paymentMethods?.includes(id) ?? false}
              onChange={(e) =>
                patch(
                  "paymentMethods",
                  e.target.checked
                    ? [...(data.paymentMethods ?? []), id]
                    : (data.paymentMethods ?? []).filter(
                        (value) => value !== id,
                      ),
                )
              }
            />
            {label}
          </label>
        ))}
        <p className="small muted">
          Провайдер оплаты:{" "}
          {initial.paymentProvider === "local"
            ? "тестовая оплата без списания"
            : initial.paymentProvider}
          . Помощник: {initial.assistantProvider}; модель:{" "}
          {initial.assistantLastModel ?? initial.assistantModel}.
        </p>
      </section>
      <section className="admin-panel">
        <h2>Содержимое сайта</h2>
        {(
          [
            ["marketingTitle", "Заголовок на главной"],
            ["menuTitle", "Заголовок меню"],
            ["menuText", "Описание меню"],
            ["video", "Ролик меню (локальный путь)"],
            ["poster", "Обложка ролика (локальный путь)"],
          ] as const
        ).map(([key, label]) => (
          <Field
            key={key}
            label={label}
            value={data[key] ?? ""}
            onChange={(e) => patch(key, e.target.value)}
          />
        ))}
        <h3>Вопросы и ответы</h3>
        {data.faq.map((item, index) => (
          <section className="admin-group" key={index}>
            <Field
              label={"Вопрос " + (index + 1)}
              value={item.question}
              required
              maxLength={150}
              onChange={(e) =>
                patch(
                  "faq",
                  data.faq.map((value, i) =>
                    i === index
                      ? { ...value, question: e.target.value }
                      : value,
                  ),
                )
              }
            />
            <label>
              Ответ
              <textarea
                value={item.answer}
                required
                maxLength={800}
                onChange={(e) =>
                  patch(
                    "faq",
                    data.faq.map((value, i) =>
                      i === index
                        ? { ...value, answer: e.target.value }
                        : value,
                    ),
                  )
                }
              />
            </label>
            <button
              className="danger-link"
              type="button"
              onClick={() =>
                patch(
                  "faq",
                  data.faq.filter((_, i) => i !== index),
                )
              }
            >
              Удалить вопрос
            </button>
          </section>
        ))}
        <button
          type="button"
          className="text-button"
          disabled={data.faq.length >= 12}
          onClick={() =>
            patch("faq", [...data.faq, { question: "", answer: "" }])
          }
        >
          Добавить вопрос
        </button>
      </section>
      <p role="status">{saved}</p>
      <button className="btn" type="submit">
        Сохранить настройки
      </button>
    </Form>
  );
}
export function ArrayField({
  label,
  value,
  change,
}: {
  label: string;
  value: string[];
  change: (value: string[]) => void;
}) {
  const [raw, setRaw] = useState(value.join(", "));
  return (
    <Field
      label={label}
      value={raw}
      onChange={(e) => {
        setRaw(e.target.value);
        change(
          e.target.value
            .split(",")
            .map((value) => value.trim())
            .filter(Boolean),
        );
      }}
    />
  );
}
function Audit({ store }: { store: ShopStore }) {
  const audit = useResource<AuditRow[]>(
    () => store.request("/api/admin/audit"),
    "audit",
  );
  return (
    <section className="admin-panel">
      <h2>Журнал действий</h2>
      {audit.error && <p className="form-error">{audit.error}</p>}
      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Дата</th>
              <th>Действие</th>
              <th>Объект</th>
              <th>Подробности</th>
            </tr>
          </thead>
          <tbody>
            {audit.value?.map((row) => (
              <tr key={row.id}>
                <td>{date(row.createdUtc)}</td>
                <td>{row.action}</td>
                <td>{row.target}</td>
                <td>{row.detail}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {audit.value?.length === 0 && <Empty title="Пока нет действий" />}
    </section>
  );
}
