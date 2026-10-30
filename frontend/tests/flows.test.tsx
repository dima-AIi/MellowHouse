import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { expect, it } from "vitest";
import userEvent from "@testing-library/user-event";
import { Auth, CartDialog } from "../src/cart-auth";
import { Checkout } from "../src/checkout";
import { OrderPage } from "../src/orders";
import { Account } from "../src/account";
import { AdminCatalogPage } from "../src/admin-catalog";
import { AdminReport } from "../src/admin-report";
import { makeStore, state, order, response, cart, report } from "./fixtures";
import type { CheckoutData } from "../src/types";

it("admin sign-in sends login required by the existing server contract", async () => {
  let submitted: unknown;
  const { store } = await makeStore((path, method, body) => {
    if (path === "/api/auth/admin" && method === "POST") {
      submitted = body;
      return response({ error: "Проверка контракта" }, 400);
    }
    return undefined;
  });
  render(<Auth store={store} admin />);
  const user = userEvent.setup();
  await user.type(
    screen.getByRole("textbox", { name: "Почта или логин" }),
    "admin@example.test",
  );
  await user.type(
    screen.getByLabelText("Пароль", { exact: true }),
    "TestPassword2026!",
  );
  await user.click(screen.getByRole("button", { name: "Войти" }));
  await screen.findByRole("alert");
  expect(submitted).toMatchObject({
    login: "admin@example.test",
    email: "admin@example.test",
    password: "TestPassword2026!",
  });
});

it("phone sign-in validates the phone, posts the code challenge, and refreshes the same cart", async () => {
  const current = structuredClone(state);
  const calls: { path: string; body: unknown }[] = [];
  const { store } = await makeStore((path, method, body) => {
    if (method === "POST") {
      calls.push({ path, body });
      if (path === "/api/auth/challenge")
        return { challengeId: "challenge", demoCode: "123456" };
      if (path === "/api/auth/verify") {
        current.authenticated = true;
        current.profile = { id: "customer", role: "Customer", name: "Гость" };
        return {};
      }
    }
    return undefined;
  }, current);
  store.authOpen = true;
  render(<Auth store={store} />);
  const user = userEvent.setup();
  await user.type(
    screen.getByRole("textbox", { name: "Телефон" }),
    "+7 999 000 00 26",
  );
  await user.click(screen.getByRole("button", { name: "Получить код" }));
  await screen.findByText(/Код: 123456/);
  await user.type(
    screen.getByRole("textbox", { name: "Код подтверждения" }),
    "123456",
  );
  await user.click(screen.getByRole("button", { name: "Подтвердить" }));
  await waitFor(() => expect(store.app.state?.authenticated).toBe(true));
  expect(calls).toEqual([
    {
      path: "/api/auth/challenge",
      body: { contact: "+7 999 000 00 26", channel: "phone" },
    },
    {
      path: "/api/auth/verify",
      body: { challengeId: "challenge", code: "123456" },
    },
  ]);
  expect(store.cartView().subtotal).toBe(300);
});
it("a wrong email password shows the server error without losing the shopping cart", async () => {
  const { store } = await makeStore((path) =>
    path === "/api/auth/login"
      ? response({ error: "Неверная почта или пароль." }, 400)
      : undefined,
  );
  store.authOpen = true;
  render(<Auth store={store} />);
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Почта и пароль" }));
  await user.type(
    screen.getByRole("textbox", { name: "Почта" }),
    "guest@example.test",
  );
  await user.type(
    screen.getByLabelText("Пароль", { exact: true }),
    "WrongPassword2026!",
  );
  await user.click(screen.getByRole("button", { name: "Войти" }));
  expect(await screen.findByRole("alert")).toHaveProperty(
    "textContent",
    "Неверная почта или пароль.",
  );
  expect(store.cartView().count).toBe(1);
});
it("cart editing sends the selected configuration and then applies only the server cart", async () => {
  const requests: unknown[] = [];
  const { store } = await makeStore((path, method, body) => {
    if (path === "/api/cart/line" && method === "PUT") {
      requests.push(body);
      return {
        ...cart,
        count: 2,
        subtotal: 600,
        items: [{ ...cart.items[0]!, quantity: 2, total: 600 }],
      };
    }
    return undefined;
  });
  store.cartOpen = true;
  render(<CartDialog store={store} />);
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Изменить" }));
  const dialog = screen.getByRole("dialog", { name: "Изменить товар" });
  await user.click(
    within(dialog).getByRole("button", { name: "Увеличить количество" }),
  );
  await user.click(
    within(dialog).getByRole("button", { name: "Сохранить изменения" }),
  );
  await waitFor(() => expect(store.cartView().count).toBe(2));
  expect(requests).toEqual([{ selections: {}, quantity: 2 }]);
});
it("checkout preserves one request identity on uncertain retry and replaces it only after changing the order", async () => {
  const submitted: CheckoutData[] = [];
  const { store } = await makeStore((path, method, body) => {
    if (path === "/api/orders" && method === "POST") {
      submitted.push(body as CheckoutData);
      return response(
        { error: "Ответ об оформлении временно недоступен" },
        503,
      );
    }
    return undefined;
  });
  render(<Checkout store={store} />);
  const user = userEvent.setup();
  await waitFor(() =>
    expect(
      (screen.getByRole("textbox", { name: "Имя" }) as HTMLInputElement)
        .disabled,
    ).toBe(false),
  );
  await user.selectOptions(
    screen.getByRole("combobox", { name: "Выберите кафе" }),
    "demo-house",
  );
  await user.type(screen.getByRole("textbox", { name: "Имя" }), "Гость");
  await user.type(
    screen.getByRole("textbox", { name: "Телефон для связи по заказу" }),
    "+79990000026",
  );
  await user.click(screen.getByRole("checkbox"));
  const button = screen.getByRole("button", { name: /Перейти к оплате/ });
  await user.click(button);
  await screen.findByText("Ответ об оформлении временно недоступен");
  await user.click(button);
  await waitFor(() => expect(submitted).toHaveLength(2));
  expect(submitted[0]!.requestKey).toBe(submitted[1]!.requestKey);
  await user.type(screen.getByRole("textbox", { name: "Имя" }), " 2");
  await user.click(button);
  await waitFor(() => expect(submitted).toHaveLength(3));
  expect(submitted[2]!.requestKey).not.toBe(submitted[1]!.requestKey);
  expect(submitted[0]!.method).toBe("pickup");
});
it("an unpaid online order is not described as cooking, and payment retries reuse the attempt key", async () => {
  history.replaceState(null, "", "/Payment/" + order.id);
  const attempts: { attemptKey: string; result: string }[] = [];
  const { store } = await makeStore((path, method, body) => {
    if (path.endsWith("/payment") && method === "POST") {
      attempts.push(body as (typeof attempts)[number]);
      return response({ error: "Ответ об оплате не получен" }, 503);
    }
    return undefined;
  });
  render(<OrderPage store={store} payment />);
  await screen.findByRole("heading", { name: "Ожидает оплаты" });
  expect(screen.queryByRole("heading", { name: "Готовится" })).toBeNull();
  const user = userEvent.setup();
  await user.click(
    screen.getByRole("button", { name: /Подтвердить тестовую оплату/ }),
  );
  await screen.findByRole("alert");
  await user.click(
    screen.getByRole("button", { name: /Подтвердить тестовую оплату/ }),
  );
  await waitFor(() => expect(attempts).toHaveLength(2));
  expect(attempts[0]!.attemptKey).toBe(attempts[1]!.attemptKey);
  history.replaceState(null, "", "/");
});
it("cancellation is available after payment and explicitly shows the remaining refund", async () => {
  history.replaceState(null, "", "/Order/" + order.id);
  const { store } = await makeStore((path) =>
    path === "/api/orders/" + order.id
      ? { ...order, status: "Paid", fulfillment: "Preparing", refunded: 100 }
      : undefined,
  );
  render(<OrderPage store={store} />);
  await screen.findByRole("heading", { name: "Готовится" });
  await userEvent.click(screen.getByRole("button", { name: "Отменить заказ" }));
  const dialog = screen.getByRole("dialog", { name: /Отменить заказ/ });
  expect(dialog.textContent).toMatch(/Возврат остатка: 200/);
  expect(
    within(dialog).getByRole("button", { name: "Оставить заказ" }),
  ).not.toBeNull();
  history.replaceState(null, "", "/");
});
it("a guest account page explains private guest links and does not expose account history", async () => {
  const { store } = await makeStore();
  render(<Account store={store} />);
  expect(
    screen.getByRole("heading", { name: "Войдите в Mellow House" }),
  ).not.toBeNull();
  expect(screen.queryByText("MH-1100")).toBeNull();
});
it("the React product editor rejects unsupported image formats before posting a file", async () => {
  const { store, fetcher } = await makeStore();
  render(<AdminCatalogPage store={store} />);
  await screen.findByText("Капучино");
  await userEvent.click(
    screen.getByRole("button", { name: "+ Добавить товар" }),
  );
  const input = document.querySelector<HTMLInputElement>('input[type="file"]')!;
  fireEvent.change(input, {
    target: {
      files: [new File(["image"], "photo.jpg", { type: "image/jpeg" })],
    },
  });
  await screen.findByText("Выберите PNG или WebP с прозрачным фоном.");
  expect(
    fetcher.mock.calls.filter(([path]) => String(path) === "/api/admin/media"),
  ).toHaveLength(0);
});
it("expense creation uses the real API and reloads the financial report", async () => {
  const seen: unknown[] = [];
  const { store } = await makeStore((path, method, body) => {
    if (path === "/api/admin/expenses" && method === "POST") {
      seen.push(body);
      return {};
    }
    return undefined;
  });
  render(<AdminReport store={store} />);
  await screen.findByText("Оплаты и возвраты по времени операции (МСК).");
  await userEvent.click(
    screen.getByRole("button", { name: "Финансы и расходы" }),
  );
  await userEvent.click(
    screen.getByRole("button", { name: "Добавить расход" }),
  );
  const dialog = screen.getByRole("dialog", { name: "Новый расход" }),
    user = userEvent.setup();
  await user.clear(
    within(dialog).getByRole("spinbutton", { name: "Сумма, ₽" }),
  );
  await user.type(
    within(dialog).getByRole("spinbutton", { name: "Сумма, ₽" }),
    "500",
  );
  await user.type(
    within(dialog).getByRole("textbox", { name: "Комментарий" }),
    "Упаковка",
  );
  await user.click(
    within(dialog).getByRole("button", { name: "Сохранить расход" }),
  );
  await waitFor(() => expect(seen).toHaveLength(1));
  expect(seen[0]).toMatchObject({
    amount: 500,
    category: "other",
    note: "Упаковка",
  });
  expect(report.collected).toBe(300);
});
