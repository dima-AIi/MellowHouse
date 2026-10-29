import { it, expect, vi } from "vitest";
import {
  BrowserCart,
  browserCartKey,
  configureLocal,
} from "../src/browser-cart";
import { coffee } from "./fixtures";
it("browser cart restores quantities, configurations and the same merge identity after a reload", () => {
  localStorage.clear();
  const first = new BrowserCart(localStorage, vi.fn());
  first.add(coffee, {}, 2);
  const id = first.read()!.id;
  const second = new BrowserCart(localStorage, vi.fn());
  expect(second.view([coffee]).subtotal).toBe(600);
  expect(second.read()!.id).toBe(id);
  second.edit(second.read()!.lines[0]!.id, coffee, {}, 3);
  expect(first.view([coffee]).count).toBe(3);
});
it("a frozen draft cannot be edited and acknowledges only the confirmed merge identity", () => {
  localStorage.clear();
  const cart = new BrowserCart(localStorage, vi.fn());
  cart.add(coffee);
  const frozen = cart.freeze()!;
  expect(() => cart.add(coffee)).toThrow(/Сверяем/);
  cart.acknowledge("other-id");
  expect(cart.read()).not.toBeNull();
  cart.acknowledge(frozen.id);
  expect(cart.read()).toBeNull();
});
it("invalid amounts and invalid options do not change the saved draft", () => {
  localStorage.clear();
  const cart = new BrowserCart(localStorage, vi.fn());
  cart.add(coffee);
  const saved = localStorage.getItem(browserCartKey);
  for (const quantity of [0, -1, 100, 1.5])
    expect(() => cart.add(coffee, {}, quantity)).toThrow();
  expect(localStorage.getItem(browserCartKey)).toBe(saved);
  expect(() =>
    configureLocal(
      {
        ...coffee,
        groups: [
          {
            id: "size",
            label: "Размер",
            multiple: false,
            required: true,
            options: [{ id: "small", label: "300 мл", price: 0 }],
            defaults: ["small"],
          },
        ],
      },
      { size: ["missing"] },
    ),
  ).toThrow(/Недоступная настройка/);
});
it("unknown and unavailable items remain visible for reconciliation", () => {
  localStorage.clear();
  const cart = new BrowserCart(localStorage, vi.fn());
  cart.add(coffee);
  const view = cart.view([]);
  expect(view.count).toBe(1);
  expect(view.items[0]!.product.available).toBe(false);
  expect(view.items[0]!.summary).toContain("Будет проверено после подключения");
});
it("merging two edited configurations preserves the combined quantity", () => {
  localStorage.clear();
  const p = {
    ...coffee,
    groups: [
      {
        id: "sauce",
        label: "Соус",
        multiple: false,
        required: true,
        options: [
          { id: "a", label: "Вместе", price: 0 },
          { id: "b", label: "Отдельно", price: 0 },
        ],
        defaults: ["a"],
      },
    ],
  };
  const cart = new BrowserCart(localStorage, vi.fn());
  cart.add(p, { sauce: ["a"] }, 2);
  cart.add(p, { sauce: ["b"] }, 1);
  cart.edit(cart.read()!.lines[1]!.id, p, { sauce: ["a"] }, 1);
  expect(cart.view([p]).items).toHaveLength(1);
  expect(cart.view([p]).count).toBe(3);
});
