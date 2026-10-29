import { useState } from "react";
import { ShopStore } from "./store";
import type { Expense, Report } from "./admin-types";
import { Field, Form, Modal, Select, useAction, useResource } from "./ui";
import { money } from "./components";
import { today } from "./sessions";
const categories: Record<string, string> = {
  rent: "Аренда",
  payroll: "Оплата труда",
  utilities: "Коммунальные услуги",
  marketing: "Маркетинг",
  delivery: "Доставка",
  other: "Прочее",
};
const firstDay = (days: number) => {
  const value = new Date(today() + "T12:00:00Z");
  value.setUTCDate(value.getUTCDate() - days + 1);
  return value.toISOString().slice(0, 10);
};
export function AdminReport({ store }: { store: ShopStore }) {
  const [from, setFrom] = useState(firstDay(30)),
    [to, setTo] = useState(today()),
    [period, setPeriod] = useState(() => `from=${firstDay(30)}&to=${today()}`),
    [tab, setTab] = useState("overview"),
    [expense, setExpense] = useState<Expense | null>(null),
    [remove, setRemove] = useState<Expense | null>(null),
    action = useAction();
  const resource = useResource<Report>(
      () => store.request("/api/admin/report?" + period),
      period,
    ),
    report = resource.value;
  const expenseTotals = Object.entries(categories).map(([category, label]) => ({
    label,
    total:
      report?.expenses
        .filter((e) => e.category === category)
        .reduce((sum, e) => sum + e.amount, 0) ?? 0,
  }));
  return (
    <>
      <div className="admin-page-heading">
        <div>
          <h1>Обзор и отчёты</h1>
          <p>Продажи, товары и затраты кафе за выбранный период.</p>
        </div>
        <a className="btn btn-outline" href={"/api/admin/report.csv?" + period}>
          Скачать CSV
        </a>
      </div>
      <div className="admin-filters">
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
          className="btn"
          onClick={() => setPeriod(`from=${from}&to=${to}`)}
        >
          Показать
        </button>
        {[
          [1, "Сегодня"],
          [7, "Неделя"],
          [30, "Месяц"],
        ].map(([days, label]) => (
          <button
            key={label}
            className="btn btn-outline"
            onClick={() => {
              const start = firstDay(Number(days));
              setFrom(start);
              setTo(today());
              setPeriod(`from=${start}&to=${today()}`);
            }}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="admin-tabs">
        {[
          ["overview", "Сводка"],
          ["sales", "Продажи"],
          ["products", "Товары"],
          ["finance", "Финансы и расходы"],
        ].map(([value, label]) => (
          <button
            key={value}
            className={tab === value ? "active" : ""}
            onClick={() => setTab(value!)}
          >
            {label}
          </button>
        ))}
      </div>
      {resource.error && (
        <p className="form-error" role="alert">
          {resource.error}
        </p>
      )}
      {!report ? (
        <p role="status">Загружаем отчёт…</p>
      ) : (
        <>
          {(tab === "overview" || tab === "sales") && (
            <>
              <div className="admin-metrics">
                {[
                  ["Поступления за вычетом возвратов", money(report.collected)],
                  ["Учтённые затраты", money(report.totalKnownExpenses)],
                  [
                    "Результат по учтённым затратам",
                    money(report.provisionalResult),
                  ],
                  ["Заказов и чеков за период", String(report.receipts)],
                ].map(([label, value]) => (
                  <article className="admin-panel" key={label}>
                    <p className="small muted">{label}</p>
                    <strong className="large-price">{value}</strong>
                  </article>
                ))}
              </div>
              <p className="small muted">{report.basis}</p>
            </>
          )}
          {tab === "overview" && (
            <>
              <h2>Работа с заказами</h2>
              <div className="admin-metrics">
                {[
                  ["Новые", report.newOrders],
                  ["В работе", report.preparing],
                  ["Готовы к выдаче", report.ready],
                  ["Завершены", report.completed],
                ].map(([label, value]) => (
                  <article className="admin-panel" key={label}>
                    <p>{label}</p>
                    <strong className="large-price">{value}</strong>
                  </article>
                ))}
              </div>
              <h2>Популярные товары</h2>
              <div className="admin-panel">
                {report.products.slice(0, 8).map((p) => (
                  <div className="report-bar-row" key={p.id}>
                    <span>{p.name}</span>
                    <div className="report-bar-track">
                      <div
                        className="report-bar"
                        style={{
                          width: `${(Math.max(0, p.quantity) / Math.max(1, ...report.products.map((row) => row.quantity))) * 100}%`,
                        }}
                      />
                    </div>
                    <strong>{p.quantity} шт.</strong>
                  </div>
                ))}
              </div>
            </>
          )}
          {tab === "sales" && (
            <div className="admin-panel">
              <h2>Продажи и возвраты</h2>
              <table className="admin-table">
                <tbody>
                  {[
                    ["Продажи в зале", report.counterRevenue],
                    ["Онлайн после возвратов", report.webRevenue],
                    ["Всего поступило", report.gross],
                    ["Возвраты", report.refunds],
                    ["Доставка", report.delivery],
                    ["Средний чек", report.average],
                  ].map(([label, value]) => (
                    <tr key={label}>
                      <th>{label}</th>
                      <td>
                        {value === null ? "Нет данных" : money(Number(value))}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {tab === "products" && (
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Товар</th>
                    <th>Количество с учётом возвратов</th>
                    <th>Выручка</th>
                    <th>Себестоимость</th>
                    <th>Маржа</th>
                  </tr>
                </thead>
                <tbody>
                  {report.products.map((p) => (
                    <tr key={p.id}>
                      <th>{p.name}</th>
                      <td>{p.quantity}</td>
                      <td>{money(p.revenue)}</td>
                      <td>
                        {p.missingCost ? "Не указана" : money(p.knownCost)}
                      </td>
                      <td>
                        {p.missingCost
                          ? "Нет данных"
                          : money(p.revenue - p.knownCost)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {tab === "finance" && (
            <>
              <div className="section-heading">
                <h2>Финансы и расходы</h2>
                <div className="admin-actions">
                  <a
                    className="btn btn-outline"
                    href={"/api/admin/finance.csv?" + period}
                  >
                    Скачать финансы CSV
                  </a>
                  <button
                    className="btn"
                    onClick={() =>
                      setExpense({
                        id: "",
                        date: today(),
                        category: "other",
                        amount: 0,
                        note: "",
                      })
                    }
                  >
                    Добавить расход
                  </button>
                </div>
              </div>
              <div className="admin-metrics">
                {[
                  ["Себестоимость учтённая", report.knownCost],
                  ["Расходы кафе", report.operatingExpenses],
                  ["Результат", report.provisionalResult],
                ].map(([label, value]) => (
                  <article className="admin-panel" key={label}>
                    <p>{label}</p>
                    <strong className="large-price">
                      {money(Number(value))}
                    </strong>
                  </article>
                ))}
              </div>
              <div className="admin-panel">
                {expenseTotals.map((row) => (
                  <p className="total-row" key={row.label}>
                    <span>{row.label}</span>
                    <strong>{money(row.total)}</strong>
                  </p>
                ))}
              </div>
              <div className="admin-table-wrap">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Дата</th>
                      <th>Категория</th>
                      <th>Сумма</th>
                      <th>Комментарий</th>
                      <th>Действия</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.expenses.map((row) => (
                      <tr key={row.id}>
                        <td>{row.date}</td>
                        <td>{categories[row.category] ?? row.category}</td>
                        <td>{money(row.amount)}</td>
                        <td>{row.note}</td>
                        <td>
                          {!row.source || row.source === "Staff" ? (
                            <>
                              <button
                                className="text-button"
                                onClick={() => setExpense(row)}
                              >
                                Изменить
                              </button>
                              <button
                                className="danger-link"
                                onClick={() => setRemove(row)}
                              >
                                В архив
                              </button>
                            </>
                          ) : (
                            <span className="small muted">
                              Учебное начисление
                            </span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}
      {expense && (
        <Modal
          open
          title={expense.id ? "Изменить расход" : "Новый расход"}
          onClose={() => setExpense(null)}
        >
          <Form
            busy={action.busy}
            error={action.error}
            onSubmit={() =>
              void action.run(async () => {
                await store.request(
                  "/api/admin/expenses" + (expense.id ? "/" + expense.id : ""),
                  expense,
                  expense.id ? "PUT" : "POST",
                );
                setExpense(null);
                await resource.reload();
              })
            }
          >
            <Field
              label="Дата расхода"
              type="date"
              value={expense.date}
              max={today()}
              required
              onChange={(e) => setExpense({ ...expense, date: e.target.value })}
            />
            <Select
              label="Категория"
              value={expense.category}
              onChange={(category) => setExpense({ ...expense, category })}
              options={Object.entries(categories)}
            />
            <Field
              label="Сумма, ₽"
              type="number"
              min={1}
              max={100000000}
              value={expense.amount}
              required
              onChange={(e) =>
                setExpense({ ...expense, amount: Number(e.target.value) })
              }
            />
            <Field
              label="Комментарий"
              maxLength={300}
              value={expense.note}
              onChange={(e) => setExpense({ ...expense, note: e.target.value })}
            />
            <button className="btn" type="submit">
              Сохранить расход
            </button>
          </Form>
        </Modal>
      )}
      <Modal
        open={!!remove}
        title="Архивировать расход?"
        onClose={() => setRemove(null)}
      >
        <p>
          {remove?.note} · {money(remove?.amount ?? 0)}
        </p>
        <p className="form-error">{action.error}</p>
        <button
          className="btn"
          disabled={action.busy}
          onClick={() =>
            void action.run(async () => {
              await store.request(
                "/api/admin/expenses/" + remove!.id,
                undefined,
                "DELETE",
              );
              setRemove(null);
              await resource.reload();
            })
          }
        >
          Подтвердить
        </button>
      </Modal>
    </>
  );
}
