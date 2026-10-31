import { fireEvent, render, screen, within } from "@testing-library/react";
import { expect, it } from "vitest";
import { Auth } from "../src/cart-auth";
import { MenuSearch } from "../src/components";
import { image, imageSet } from "../src/media";
import { validPhone } from "../src/sessions";
import { makeStore, catalog } from "./fixtures";

it.each(["123", "799900000000", "abc79990000026", "+7 999 000 00 26abc", "++79990000026"])("rejects malformed phone %s", value => {
  expect(validPhone(value)).toBe(false);
});
it.each(["+7 (999) 000-00-26", "8 (999) 000-00-26", "79990000026"])("accepts complete phone %s", value => {
  expect(validPhone(value)).toBe(true);
});
it("rejects an invalid sign-in phone inline without posting a challenge", async () => {
  const { store, fetcher } = await makeStore();
  store.authOpen = true;
  render(<Auth store={store} />);
  const input = screen.getByRole("textbox", {name: "Телефон"});
  fireEvent.change(input, {target: {value: "12345"}});
  fireEvent.click(screen.getByRole("button", {name: "Получить код"}));
  expect(input.getAttribute("aria-invalid")).toBe("true");
  expect(input).toBe(document.activeElement);
  expect(screen.getByRole("alert").textContent).toContain("Введите полный номер");
  expect(fetcher.mock.calls.some(([path]) => path === "/api/auth/challenge")).toBe(false);
  fireEvent.change(input, {target: {value: "+7 (999) 000-00-26"}});
  expect(input.hasAttribute("aria-invalid")).toBe(false);
});
it("renders a visible empty-result explanation and recovers on the next search", () => {
  render(<MenuSearch catalog={catalog} />);
  const input = screen.getByRole("searchbox");
  fireEvent.change(input, {target: {value: "несуществующее блюдо"}});
  const results = screen.getByRole("region", {name: "Результаты поиска"});
  expect(within(results).getByText("Результаты не найдены")).toBeTruthy();
  fireEvent.change(input, {target: {value: "Капучино"}});
  expect(within(results).queryByText("Результаты не найдены")).toBeNull();
  expect(within(results).getByRole("link", {name: /Капучино/})).toBeTruthy();
});
it("serves responsive category images and preserves unknown uploaded images", () => {
  expect(imageSet("/images/categories/cappuccino.webp")).toContain("-320.webp 320w");
  expect(image("/media/custom.webp", 640)).toBe("/media/custom.webp");
  expect(imageSet("/media/custom.webp")).toBe("");
});

it("limits typed or pasted phone input to eleven digits and removes the welcome heading", async () => {
  const { store } = await makeStore();
  store.authOpen = true;
  render(<Auth store={store} />);
  const input = screen.getByRole("textbox", {name: "Телефон"}) as HTMLInputElement;
  fireEvent.change(input, {target: {value: "555555555555555555555555"}});
  expect(input.value).toBe("55555555555");
  fireEvent.change(input, {target: {value: "+7 (999) 000-00-261234"}});
  expect(input.value.replace(/\D/g, "")).toBe("79990000026");
  expect(screen.queryByText("Добро пожаловать в Mellow House")).toBeNull();
});
