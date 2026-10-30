import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import userEvent from "@testing-library/user-event";
import { PublicApp } from "../src/public-app";
import { Account } from "../src/account";
import { Checkout } from "../src/checkout";
import { AdminCatalogPage } from "../src/admin-catalog";
import { ShopStore } from "../src/store";
import { publicCacheKey, writePublicCache } from "../src/public-cache";
import { catalog, checkout, makeStore, response, state } from "./fixtures";
beforeEach(() => {
  localStorage.clear();
  history.replaceState(null, "", "/");
});
afterEach(() => {
  document.documentElement.removeAttribute("data-static-storefront");
  cleanup();
  vi.useRealTimers();
});

it("a warm tab needs one session request and never repeats the cart read", async () => {
  const { fetcher } = await makeStore();
  expect(
    fetcher.mock.calls.filter(([path]) => path === "/api/state"),
  ).toHaveLength(1);
  expect(
    fetcher.mock.calls.filter(([path]) => path === "/api/cart"),
  ).toHaveLength(0);
});
it("admin startup does not wait for the storefront catalog or cart", async () => {
  const paths: string[] = [];
  const store = new ShopStore(
    async (input) => {
      paths.push(String(input));
      return response(state);
    },
    localStorage,
    { catalog: false },
  );
  await store.ready;
  expect(paths).toEqual(["/api/state"]);
  expect(store.phase).toBe("connected");
});
it("a delayed live catalog does not block an established session or replace the visible menu", async () => {
  document.documentElement.setAttribute("data-static-storefront", "true");
  let finish!: (value: Response) => void;
  const store = new ShopStore(
    async (input) =>
      String(input) === "/api/state"
        ? response(state)
        : String(input) === "/api/catalog"
          ? new Promise<Response>((resolve) => {
              finish = resolve;
            })
          : response(catalog),
    localStorage,
  );
  await store.ready;
  expect(store.phase).toBe("connected");
  expect(store.cartView().count).toBe(state.cart.count);
  expect(store.app.catalog?.products).toEqual(catalog.products);
  finish(response(catalog));
});
it("cached public cards stay mounted during connection and a fast connection never flashes the banner", async () => {
  vi.useFakeTimers();
  history.replaceState(null, "", "/Menu");
  writePublicCache(localStorage, {
    catalog,
    settings: state.settings,
    savedAt: Date.now(),
  });
  let wake!: (value: Response) => void;
  const store = new ShopStore(
    async () =>
      new Promise<Response>((resolve) => {
        wake = resolve;
      }),
    localStorage,
  );
  render(<PublicApp store={store} template="" />);
  const card = screen.getByRole("button", {
    name: "Добавить Капучино в корзину",
  });
  expect(document.querySelector(".server-connection")).toBeNull();
  await act(() => vi.advanceTimersByTimeAsync(2400));
  expect(document.querySelector(".server-connection")).toBeNull();
  await act(async () => {
    wake(response(state));
    await store.ready;
  });
  expect(
    screen.getByRole("button", { name: "Добавить Капучино в корзину" }),
  ).toBe(card);
  expect(document.querySelector(".server-connection")).toBeNull();
  expect(screen.queryByText("Время для любимого")).toBeNull();
});
it("public cache contains no account, token, order history or server cart", async () => {
  const initial = structuredClone(state);
  initial.authenticated = true;
  initial.csrfToken = "PRIVATE-CSRF";
  initial.profile = {
    id: "private-id",
    role: "Customer",
    name: "PRIVATE-NAME",
  };
  await makeStore(undefined, initial);
  const saved = localStorage.getItem(publicCacheKey)!;
  expect(saved).toContain("Капучино");
  expect(saved).not.toMatch(
    /PRIVATE-CSRF|PRIVATE-NAME|"profile"|"cart"|"csrfToken"|"orders"/,
  );
});
it.each(["timing", "scheduledLocal"])(
  "the server %s error is announced and focused next to receiving time",
  async (field) => {
    const message = "Кафе закрыто. Выберите время с 08:00 до 22:00 (МСК).";
    const { store } = await makeStore((path, method) =>
      path === "/api/checkout-draft"
        ? checkout
        : path === "/api/orders" && method === "POST"
          ? response({ error: message, field }, 400)
          : undefined,
    );
    render(<Checkout store={store} />);
    const button = screen.getByRole("button", { name: /Перейти к оплате/ });
    await waitFor(() =>
      expect(button.closest("fieldset")?.disabled).toBe(false),
    );
    fireEvent.submit(button.closest("form")!);
    const error = await screen.findByRole("alert");
    const group = screen.getByRole("group", { name: "Время получения" });
    expect(group.contains(error)).toBe(true);
    expect(error.textContent).toBe(message);
    expect(document.activeElement).toBe(group);
    expect(screen.queryByText("1 Получение")).toBeNull();
    expect(screen.queryByText("2 Способ оплаты")).toBeNull();
  },
);
it("account links use the original sidebar beside the history rather than stretching above it", async () => {
  const initial = structuredClone(state);
  initial.authenticated = true;
  initial.profile = { id: "customer", role: "Customer", name: "Гость" };
  const { store } = await makeStore(undefined, initial);
  render(<Account store={store} />);
  await screen.findByRole("heading", { name: "Заказ MH-1100" });
  const sidebar = screen.getByRole("complementary", { name: "Личный кабинет" });
  expect(sidebar.closest(".account-grid")).not.toBeNull();
  expect(
    sidebar.parentElement?.querySelector("#account-content"),
  ).not.toBeNull();
  expect(
    within(sidebar).getByRole("link", { name: "Мои заказы" }),
  ).not.toBeNull();
  expect(
    screen.getByRole("navigation", { name: "Разделы меню" }),
  ).not.toBeNull();
});
it("catalog restores product cards, distinct edit/copy/archive controls and working price sorting", async () => {
  const { store } = await makeStore();
  render(<AdminCatalogPage store={store} />);
  await screen.findByRole("heading", { name: "Капучино" });
  const user = userEvent.setup();
  await user.selectOptions(
    screen.getByRole("combobox", { name: "Сортировка" }),
    "price-desc",
  );
  const cards = document.querySelectorAll(".admin-product-list article");
  expect(cards[0]?.textContent).toContain("Пицца Маргарита");
  const row = within(cards[0] as HTMLElement);
  expect(row.getByRole("button", { name: "Изменить" }).className).toBe(
    "btn btn-outline",
  );
  expect(row.getByRole("button", { name: "Дублировать" })).not.toBeNull();
  expect(row.getByRole("button", { name: "В архив" }).className).toBe(
    "danger-link",
  );
});
