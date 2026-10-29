import { it, expect, vi } from "vitest";
import { makeStore, coffee, response, state, catalog } from "./fixtures";
import { ShopStore } from "../src/store";
it("native fetch receives no store instance as its receiver", async () => {
  const receivers: unknown[] = [];
  const fetcher = async function (this: unknown, input: RequestInfo | URL) {
    receivers.push(this);
    return response(
      String(input) === "/api/state"
        ? state
        : String(input) === "/api/catalog"
          ? catalog
          : state.cart,
    );
  };
  const store = new ShopStore(fetcher as typeof fetch, localStorage);
  await store.ready;
  expect(receivers.every((value) => value === undefined)).toBe(true);
});
it("rapid product clicks are queued, and an older cart cannot overwrite a newer revision", async () => {
  let revision = 1;
  const deltas: number[] = [];
  const { store } = await makeStore(async (path, method, body) => {
    if (path.startsWith("/api/cart/product/") && method === "POST") {
      deltas.push((body as { delta: number }).delta);
      await Promise.resolve();
      return { ...state.cart, count: ++revision, revision };
    }
    return undefined;
  });
  await Promise.all([
    store.adjust("coffee", 1),
    store.adjust("pizza", 1),
    store.adjust("coffee", -1),
  ]);
  expect(deltas).toEqual([1, 1, -1]);
  expect(store.cartView().revision).toBe(4);
  store.setCart({ ...state.cart, revision: 2 });
  expect(store.cartView().revision).toBe(4);
});
it("one session establishes its cookie before owner-sensitive catalog and cart requests", async () => {
  const { store, fetcher } = await makeStore();
  expect(String(fetcher.mock.calls[0]![0])).toBe("/api/state");
  await store.add(coffee);
  const call = fetcher.mock.calls.find(
    ([, options]) => options?.method === "POST",
  );
  expect(call?.[1]?.headers).toMatchObject({ "X-CSRF-TOKEN": "csrf-test" });
  expect(call?.[1]?.credentials).toBe("same-origin");
});
it("a JSON validation failure preserves the field and is never treated as success", async () => {
  const { store } = await makeStore((path, method) =>
    path === "/api/orders" && method === "POST"
      ? response({ error: "Проверьте телефон", field: "phone" }, 400)
      : undefined,
  );
  await expect(store.request("/api/orders", {}, "POST")).rejects.toMatchObject({
    status: 400,
    field: "phone",
    message: "Проверьте телефон",
  });
});
it("cold-start HTML cannot be mistaken for a successful API mutation", async () => {
  const { store } = await makeStore((path) =>
    path === "/api/orders"
      ? new Response("<html>Render</html>", {
          status: 200,
          headers: { "Content-Type": "text/html" },
        })
      : undefined,
  );
  await expect(store.request("/api/orders", {}, "POST")).rejects.toMatchObject({
    status: 503,
  });
});
it("a transparent photo upload retains the file and CSRF without a JSON content type", async () => {
  const { store, fetcher } = await makeStore();
  const form = new FormData();
  form.set("file", new File(["test"], "test.png", { type: "image/png" }));
  form.set("kind", "product");
  await store.request("/api/admin/media", form, "POST");
  const options = fetcher.mock.calls.at(-1)![1]!;
  expect(options.body).toBe(form);
  expect(options.headers).not.toHaveProperty("Content-Type");
  expect(options.headers).toHaveProperty("X-CSRF-TOKEN");
});
it("before the server wakes a static catalog can collect items and merge them exactly once", async () => {
  localStorage.clear();
  document.documentElement.setAttribute("data-static-storefront", "true");
  let wake!: (value: Response) => void;
  const calls: string[] = [];
  const fetcher = vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    calls.push(path);
    if (path === "/api/state")
      return new Promise<Response>((resolve) => {
        wake = resolve;
      });
    if (path === "/catalog.json" || path === "/api/catalog")
      return response(catalog);
    if (path === "/api/cart/browser-sync")
      return response({
        cart: { items: [], count: 2, subtotal: 600, revision: 1 },
        warnings: [],
      });
    return response(state.cart);
  });
  const store = new ShopStore(fetcher as typeof fetch, localStorage);
  await store.catalogReady;
  await store.add(coffee, {}, 2);
  expect(store.cartView().count).toBe(2);
  expect(store.phase).toBe("connecting");
  wake(response(state));
  await store.ready;
  expect(
    calls.filter((path) => path === "/api/cart/browser-sync"),
  ).toHaveLength(1);
  expect(store.cartView().count).toBe(2);
  document.documentElement.removeAttribute("data-static-storefront");
});
