import { vi } from "vitest";
import { ShopStore } from "../src/store";
import type {
  Cart,
  Catalog,
  CheckoutData,
  Order,
  Product,
  SessionState,
} from "../src/types";
import type { AdminCatalog, Report } from "../src/admin-types";
export const coffee: Product = {
  id: "coffee",
  categoryId: "coffee",
  name: "Капучино",
  price: 300,
  portion: "300 мл",
  description: "Кофе с молоком",
  ingredients: "Кофе, молоко",
  image: "/images/products/cappuccino.png",
  available: true,
  popular: true,
  recommended: true,
  groups: [],
  allergens: ["молоко"],
};
export const pizza: Product = {
  ...coffee,
  id: "pizza",
  categoryId: "pizza",
  name: "Пицца Маргарита",
  price: 520,
};
export const catalog: Catalog = {
  products: [coffee, pizza],
  categories: [
    { id: "coffee", name: "Кофе", image: "", onHome: false, sortOrder: 0 },
    { id: "pizza", name: "Пицца", image: "", onHome: false, sortOrder: 1 },
  ],
};
export const cart: Cart = {
  items: [
    {
      id: "line",
      product: coffee,
      quantity: 1,
      selections: {},
      unitPrice: 300,
      total: 300,
      summary: [],
    },
  ],
  count: 1,
  subtotal: 300,
  revision: 1,
};
export const checkout: CheckoutData = {
  requestKey: "checkout-key",
  method: "pickup",
  city: "",
  street: "",
  apartment: "",
  name: "Тестовый гость",
  phone: "+79990000026",
  cafe: "demo-house",
  timing: "asap",
  scheduledLocal: "",
  comment: "",
  paymentMethod: "card",
  consent: true,
};
export const order: Order = {
  id: "11111111-1111-4111-8111-111111111111",
  number: "MH-1100",
  status: "Pending",
  items: [
    {
      productId: "coffee",
      name: "Капучино",
      image: coffee.image,
      portion: "300 мл",
      configuration: "{}",
      summary: [],
      quantity: 1,
      unitPrice: 300,
      total: 300,
    },
  ],
  checkout,
  subtotal: 300,
  delivery: 0,
  total: 300,
  createdUtc: "2026-10-30T09:00:00Z",
  paymentMethod: "card",
  fulfillment: "New",
  refunded: 0,
  source: "Guest",
  allowedTransitions: [],
  guest: true,
  canCancel: true,
  events: [],
  refunds: [],
};
export const adminCatalog: AdminCatalog = {
  categories: catalog.categories.map((c) => ({
    ...c,
    visible: true,
    archived: false,
  })),
  products: catalog.products.map((p) => ({
    ...p,
    draft: false,
    archived: false,
    sortOrder: 0,
    cost: 90,
    serving: 300,
    unit: "мл",
  })),
};
export const state: SessionState = {
  csrfToken: "csrf-test",
  authenticated: false,
  profile: null,
  favorites: [],
  cart,
  deliveryPrice: 190,
  settings: {
    name: "Mellow House",
    cafeName: "Mellow House",
    phone: "",
    email: "",
    address: "Москва",
    hours: "08:00–22:00",
    openingTime: "08:00",
    closingTime: "22:00",
    deliveryCities: ["Москва"],
    closedDates: [],
    closedWeekdays: [],
    paymentMethods: ["card", "sbp", "cash", "terminal"],
    deliveryPrice: 190,
    marketingTitle: "Ваш кофе. Ваша пауза.",
    menuTitle: "Наше меню",
    menuText: "Выберите любимое.",
    faq: [],
  },
};
export const report: Report = {
  from: "2026-10-01",
  to: "2026-10-30",
  basis: "Оплаты и возвраты по времени операции (МСК).",
  collected: 300,
  totalKnownExpenses: 90,
  provisionalResult: 210,
  netProfit: 210,
  created: 1,
  receipts: 1,
  newOrders: 1,
  preparing: 0,
  ready: 0,
  completed: 0,
  rejected: 0,
  gross: 300,
  refunds: 0,
  delivery: 0,
  average: 300,
  knownCost: 90,
  missingCost: false,
  operatingExpenses: 0,
  counterRevenue: 0,
  webRevenue: 300,
  products: [
    {
      id: "coffee",
      name: "Капучино",
      quantity: 1,
      revenue: 300,
      knownCost: 90,
      missingCost: false,
    },
  ],
  expenses: [],
};
export type Handler = (
  path: string,
  method: string,
  body: unknown,
) => unknown | Promise<unknown>;
export function response(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
export async function makeStore(
  handler?: Handler,
  initial: SessionState = structuredClone(state),
) {
  const fetcher = vi.fn(
    async (input: RequestInfo | URL, options?: RequestInit) => {
      const path = String(input),
        method = options?.method ?? "GET";
      const body =
        typeof options?.body === "string"
          ? (JSON.parse(options.body) as unknown)
          : options?.body;
      const custom = handler ? await handler(path, method, body) : undefined;
      if (custom !== undefined)
        return custom instanceof Response ? custom : response(custom);
      if (path === "/api/state") return response(initial);
      if (path === "/api/catalog" || path === "/catalog.json")
        return response(catalog);
      if (path === "/api/cart") return response(initial.cart);
      if (path === "/api/checkout-draft") return response({});
      if (path === "/api/admin/catalog") return response(adminCatalog);
      if (path === "/api/admin/clients") return response([]);
      if (path.startsWith("/api/admin/report?")) return response(report);
      if (path === "/api/admin/settings")
        return response({
          site: initial.settings,
          paymentProvider: "local",
          assistantProvider: "local",
          assistantModel: "local",
          assistantModels: [],
        });
      if (path === "/api/admin/audit") return response([]);
      if (path === "/api/orders" || path === "/api/admin/orders")
        return response([order]);
      if (path === "/api/orders/" + order.id) return response(order);
      if (path === "/api/admin/orders/" + order.id)
        return response({ order, attempts: [] });
      return response({});
    },
  );
  const store = new ShopStore(fetcher as typeof fetch, localStorage);
  await store.ready;
  return { store, fetcher, initial };
}
