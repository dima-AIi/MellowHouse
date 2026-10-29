import { useSyncExternalStore } from "react";
import { BrowserCart, browserCartKey } from "./browser-cart";
import { syncOwner, clearChat } from "./sessions";
import type {
  Catalog,
  Cart,
  ShopRuntime,
  RuntimeState,
  Selections,
  SessionState,
  Product,
} from "./types";

export class ApiError extends Error {
  constructor(
    message: string,
    public status = 0,
    public field?: string,
  ) {
    super(message);
  }
}
export async function retryRead<T>(read: () => Promise<T>, timeout = 90000) {
  const start = Date.now();
  for (;;) {
    try {
      return await read();
    } catch (error) {
      if (
        (error instanceof ApiError &&
          error.status > 0 &&
          error.status < 500 &&
          error.status !== 429) ||
        Date.now() - start >= timeout
      )
        throw error;
      await new Promise((resolve) => setTimeout(resolve, 2500));
    }
  }
}
export class ShopStore implements ShopRuntime {
  app: RuntimeState = { state: null, catalog: null, method: "pickup" };
  phase: "connecting" | "connected" | "failed" = "connecting";
  notice = "";
  message = "";
  cartOpen = false;
  authOpen = false;
  afterAuth?: () => void;
  ready: Promise<unknown>;
  catalogReady: Promise<Catalog>;
  private listeners = new Set<() => void>();
  private revision = 0;
  private local: BrowserCart;
  private mutations: Promise<void> = Promise.resolve();
  private ownerEpoch = 0;
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  snapshot = () => this.revision;
  emit = () => {
    this.revision++;
    for (const listener of this.listeners) listener();
  };
  constructor(
    private fetcher: typeof fetch = fetch,
    private storage: Storage = localStorage,
  ) {
    this.app.method =
      this.storage.getItem("mh.method") === "delivery" ? "delivery" : "pickup";
    this.local = new BrowserCart(storage, this.emit);
    const state = retryRead(() => this.refresh());
    this.catalogReady = (
      document.documentElement.hasAttribute("data-static-storefront")
        ? this.request<Catalog>("/catalog.json")
        : state.then(() =>
            retryRead(() => this.request<Catalog>("/api/catalog")),
          )
    ).then((c) => {
      this.app.catalog = c;
      this.emit();
      return c;
    });
    const initialCatalog = this.catalogReady,
      local = this.local;
    this.ready = (async () => {
      try {
        await state;
        await initialCatalog;
        if (document.documentElement.hasAttribute("data-static-storefront"))
          this.app.catalog = await retryRead(() =>
            this.request<Catalog>("/api/catalog"),
          );
        const batch = local.freeze();
        if (batch) {
          const result = await retryRead(() =>
            this.request<{ cart: Cart; warnings: string[] }>(
              "/api/cart/browser-sync",
              {
                requestKey: batch.id,
                lines: batch.lines.map((line) => ({
                  productId: line.productId,
                  selections: line.selections,
                  quantity: line.quantity,
                  expectedUnitPrice: line.expectedUnitPrice,
                })),
              },
              "POST",
            ),
          );
          this.app.state!.cart = result.cart;
          this.notice = result.warnings.join(" ");
        } else
          this.app.state!.cart = await retryRead(() =>
            this.request<Cart>("/api/cart"),
          );
        this.phase = "connected";
        if (batch) local.acknowledge(batch.id);
        this.emit();
        return this;
      } catch (error) {
        this.phase = "failed";
        this.emit();
        throw error;
      }
    })();
    // Every consumer has a visible failed state; do not leave rejected startup promises unhandled.
    void this.ready.catch(() => {});
    void this.catalogReady.catch(() => {});
    window.addEventListener("storage", (event) => {
      if (event.key === browserCartKey) this.emit();
    });
  }
  async request<T>(path: string, body?: unknown, method = "GET"): Promise<T> {
    let response: Response;
    const upload = body instanceof FormData;
    const fetcher = this.fetcher;
    try {
      response = await fetcher(path, {
        method,
        credentials: "same-origin",
        headers: {
          Accept: "application/json",
          ...(body !== undefined && !upload
            ? { "Content-Type": "application/json" }
            : {}),
          ...(method !== "GET"
            ? { "X-CSRF-TOKEN": this.app.state?.csrfToken ?? "" }
            : {}),
        },
        ...(body !== undefined
          ? { body: upload ? body : JSON.stringify(body) }
          : {}),
        signal: AbortSignal.timeout(path === "/api/assistant" ? 55000 : 15000),
      });
    } catch {
      throw new ApiError(
        "Не удалось связаться с кафе. Проверьте соединение. Корзина сохранена.",
      );
    }
    if (!response.headers.get("content-type")?.includes("application/json"))
      throw new ApiError(
        "Кафе подключается. Повторите действие после подключения.",
        response.ok ? 503 : response.status,
      );
    const data: unknown = await response.json();
    if (!response.ok) {
      const error = data as { error?: string; field?: string };
      throw new ApiError(
        error.error ?? "Не удалось выполнить действие.",
        response.status,
        error.field,
      );
    }
    return data as T;
  }
  async refresh() {
    const state = await this.request<SessionState>("/api/state");
    if (
      this.app.state &&
      (this.app.state.profile?.id !== state.profile?.id ||
        this.app.state.authenticated !== state.authenticated)
    )
      this.ownerEpoch++;
    syncOwner(state.authenticated ? "u:" + state.profile!.id : "guest");
    this.app.state = state;
    this.emit();
    return state;
  }
  cartView = (): Cart =>
    this.phase === "connected" && this.app.state
      ? this.app.state.cart
      : this.local.view(this.app.catalog?.products);
  setCart = (cart: Cart) => {
    if (
      this.app.state &&
      (cart.revision ?? 0) >= (this.app.state.cart.revision ?? 0)
    )
      this.app.state.cart = cart;
    this.emit();
  };
  updateState = this.emit;
  defaults = (product: Product): Selections =>
    Object.fromEntries(
      product.groups.map((group) => [group.id, [...group.defaults]]),
    );
  calculated = (product: Product, selection: Selections) => {
    let price = product.price;
    const allergens = new Set(product.allergens),
      summary: string[] = [];
    for (const group of product.groups)
      for (const id of selection[group.id] ?? []) {
        const option = group.options.find((o) => o.id === id);
        if (!option) continue;
        price += option.price;
        summary.push(option.label);
        for (const value of option.removeAllergens ?? [])
          allergens.delete(value);
        for (const value of option.addAllergens ?? []) allergens.add(value);
      }
    return { price, allergens: [...allergens], summary };
  };
  toast = (message: string) => {
    this.message = message;
    this.emit();
    setTimeout(() => {
      if (this.message === message) {
        this.message = "";
        this.emit();
      }
    }, 5500);
  };
  openAuth = (after?: () => void) => {
    this.afterAuth = after;
    this.authOpen = true;
    this.emit();
  };
  signedIn = async () => {
    await this.refresh();
    this.authOpen = false;
    this.emit();
    const callback = this.afterAuth;
    this.afterAuth = undefined;
    callback?.();
  };
  async logout() {
    await this.request("/api/auth/logout", {}, "POST");
    this.storage.removeItem(browserCartKey);
    clearChat(sessionStorage);
    document.dispatchEvent(new CustomEvent("mh:chat-reset"));
    await this.refresh();
    this.cartOpen = false;
    this.authOpen = false;
    this.emit();
  }
  async setMethod(method: "pickup" | "delivery") {
    this.app.method = method;
    this.storage.setItem("mh.method", method);
    this.emit();
    if (this.phase === "connected")
      await this.request("/api/fulfillment", { method }, "PUT");
  }
  private mutateCart(path: string, body?: unknown, method = "POST") {
    const epoch = this.ownerEpoch;
    const work = this.mutations.then(async () => {
      if (epoch !== this.ownerEpoch)
        throw Error("Аккаунт изменился. Повторите действие.");
      const cart = await this.request<Cart>(path, body, method);
      if (epoch === this.ownerEpoch) this.setCart(cart);
    });
    this.mutations = work.catch(() => {});
    return work;
  }
  async add(
    product: Product,
    selections = this.defaults(product),
    quantity = 1,
  ) {
    await this.catalogReady;
    if (this.phase !== "connected") {
      this.local.add(product, selections, quantity);
      return;
    }
    await this.mutateCart("/api/cart", {
      productId: product.id,
      selections,
      quantity,
    });
  }
  async edit(id: string, selections: Selections, quantity: number) {
    if (this.phase !== "connected") {
      const line = this.cartView().items.find((l) => l.id === id);
      if (!line) throw Error("Позиция изменилась.");
      this.local.edit(id, line.product, selections, quantity);
      return;
    }
    await this.mutateCart("/api/cart/" + id, { selections, quantity }, "PUT");
  }
  async remove(id?: string) {
    if (this.phase !== "connected") {
      this.local.remove(id);
      return;
    }
    await this.mutateCart(
      "/api/cart" + (id ? "/" + id : ""),
      undefined,
      "DELETE",
    );
  }
  async adjust(id: string, delta: number) {
    await this.catalogReady;
    if (this.phase === "connected") {
      await this.mutateCart("/api/cart/product/" + encodeURIComponent(id), {
        delta,
      });
      return;
    }
    const lines = this.cartView().items.filter((l) => l.product.id === id);
    if (lines.length > 1) {
      this.cartOpen = true;
      this.emit();
      throw Error("Выберите нужный вариант товара в корзине.");
    }
    const line = lines[0];
    if (!line) {
      if (delta > 0) {
        const p = this.app.catalog?.products.find((p) => p.id === id);
        if (p) await this.add(p);
      }
      return;
    }
    if (line.quantity + delta < 1) await this.remove(line.id);
    else await this.edit(line.id, line.selections, line.quantity + delta);
  }
  async favorite(id: string) {
    await this.ready;
    const selected = !this.app.state!.favorites.includes(id);
    await this.request("/api/favorites/" + id, { selected }, "PUT");
    this.app.state!.favorites = selected
      ? [...this.app.state!.favorites, id]
      : this.app.state!.favorites.filter((value) => value !== id);
    this.emit();
  }
}
export function useShop(store: ShopStore) {
  useSyncExternalStore(store.subscribe, store.snapshot, store.snapshot);
  return store;
}
