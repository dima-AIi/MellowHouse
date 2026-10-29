import { it, expect } from "vitest";
import { order } from "./fixtures";
import { orderStatus } from "../src/order-state";
for (const method of ["pickup", "delivery"] as const) {
  it(
    method + ": only the saved ready stage controls the pickup/courier wording",
    () => {
      const result = orderStatus({
        ...order,
        status: "Paid",
        fulfillment: "Ready",
        checkout: { ...order.checkout, method },
      });
      expect(result.title).toBe(
        method === "delivery" ? "Готов к отправке" : "Готов к выдаче",
      );
      expect(result.steps.some((step) => step.label === "У курьера")).toBe(
        method === "delivery",
      );
    },
  );
  it(
    method + ": completed is issued or delivered according to fulfillment",
    () => {
      expect(
        orderStatus({
          ...order,
          status: "Paid",
          fulfillment: "Completed",
          checkout: { ...order.checkout, method },
        }).title,
      ).toBe(method === "delivery" ? "Доставлен" : "Выдан");
    },
  );
  it(
    method +
      ": payment-at-receipt is an actual submitted order, not a payment error",
    () => {
      expect(
        orderStatus({
          ...order,
          paymentMethod: "cash",
          checkout: { ...order.checkout, method },
        }).title,
      ).toBe("Заказ получен");
    },
  );
}
it("failed online payments and refunded or cancelled orders never become cooking states", () => {
  for (const status of ["Pending", "Failed", "Cancelled"])
    expect(orderStatus({ ...order, status }).title).not.toBe("Готовится");
  expect(
    orderStatus({
      ...order,
      status: "Paid",
      fulfillment: "Cancelled",
      refunded: 300,
    }).title,
  ).toBe("Заказ отменён");
  expect(orderStatus({ ...order, status: "Refunded" }).title).toBe(
    "Оплата возвращена",
  );
});
