import type { Order } from "./types";
export function orderStatus(order: Order) {
  const delivery = order.checkout.method === "delivery",
    paid = ["Paid", "PartiallyRefunded", "Refunded"].includes(order.status),
    receipt = ["cash", "terminal"].includes(order.paymentMethod);
  if (order.fulfillment === "Cancelled")
    return {
      title: "Заказ отменён",
      note: order.refunded
        ? "Отмена подтверждена. Возврат тестовой оплаты учтён; подробности — в истории заказа."
        : "Кафе не будет готовить этот заказ. Оплачивать его не нужно.",
      steps: [],
    };
  if (order.fulfillment === "Rejected")
    return {
      title: "Заказ отклонён",
      note: "Причина указана в истории ниже. Свяжитесь с кафе, если нужна помощь.",
      steps: [],
    };
  if (order.status === "Refunded")
    return {
      title: "Оплата возвращена",
      note: "Полный возврат оплаты отмечен. Подробности указаны в истории заказа.",
      steps: [],
    };
  if (!paid && !receipt)
    return {
      title: ["Failed", "Cancelled"].includes(order.status)
        ? "Оплата не завершена"
        : "Ожидает оплаты",
      note: "Заказ сохранён. Кафе начнёт его готовить после подтверждения оплаты.",
      steps: [
        { label: "Оплата", state: "current" },
        { label: "Приготовление", state: "next" },
        { label: delivery ? "Доставка" : "Получение", state: "next" },
      ],
    };
  const stages = [
    ["New", "Получен"],
    ["Accepted", "Принят"],
    ["Preparing", "Готовится"],
    ["Ready", delivery ? "Готов к отправке" : "Готов к выдаче"],
    ...(delivery ? [["OutForDelivery", "У курьера"]] : []),
    ["Completed", delivery ? "Доставлен" : "Выдан"],
  ];
  const index = Math.max(
    0,
    stages.findIndex(([code]) => code === order.fulfillment),
  );
  const notes: Record<string, string> = {
    New: "Заказ передан в кафе и ожидает подтверждения.",
    Accepted: "Кафе приняло заказ. Скоро начнём готовить.",
    Preparing: "На кухне готовят ваши блюда и напитки.",
    Ready: delivery
      ? "Заказ готов. Ожидаем передачи курьеру."
      : "Заказ готов. Можно забрать его в кафе.",
    OutForDelivery: "Курьер забрал заказ и направляется по указанному адресу.",
    Completed: delivery
      ? "Заказ доставлен. Приятного аппетита!"
      : "Заказ выдан. Приятного аппетита!",
  };
  return {
    title: order.fulfillment === "New" ? "Заказ получен" : stages[index]![1]!,
    note: notes[order.fulfillment] ?? notes.New!,
    steps: stages.map(([, label], i) => ({
      label: label!,
      state: i < index ? "complete" : i === index ? "current" : "next",
    })),
  };
}
