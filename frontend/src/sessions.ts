import type { ChatSession, Order } from "./types";
export const chatKey = "mh.chat.v10";
export function readChat(storage: Storage): ChatSession | null {
  try {
    const value = JSON.parse(
      storage.getItem(chatKey) ?? "null",
    ) as ChatSession & { version?: number };
    return value?.version === 1 &&
      typeof value.owner === "string" &&
      Array.isArray(value.entries) &&
      value.entries.length <= 80
      ? value
      : null;
  } catch {
    return null;
  }
}
export function writeChat(storage: Storage, data: ChatSession) {
  try {
    storage.setItem(
      chatKey,
      JSON.stringify({ ...data, version: 1, entries: data.entries.slice(-80) }),
    );
  } catch {}
}
export function clearChat(storage: Storage) {
  try {
    storage.removeItem(chatKey);
  } catch {}
}
export function syncOwner(owner: string) {
  const saved = readChat(sessionStorage);
  if (!saved || saved.owner === owner) return;
  if (saved.owner === "guest" && owner !== "guest") {
    writeChat(sessionStorage, { ...saved, owner });
    return;
  }
  clearChat(sessionStorage);
  document.dispatchEvent(new CustomEvent("mh:chat-reset"));
}
export function rememberOrder(order: Pick<Order, "id" | "guestAccessToken">) {
  if (order.guestAccessToken)
    try {
      sessionStorage.setItem(
        "mh.guest-order." + order.id,
        order.guestAccessToken,
      );
      sessionStorage.setItem("mh.guest-last", order.id);
    } catch {}
}
export function orderUrl(id: string, page = "Order") {
  let token = "";
  try {
    token = sessionStorage.getItem("mh.guest-order." + id) ?? "";
  } catch {}
  return `/${page}/${id}${token ? "#access=" + encodeURIComponent(token) : ""}`;
}
export function forgetOrder(id: string) {
  sessionStorage.removeItem("mh.guest-order." + id);
  if (sessionStorage.getItem("mh.guest-last") === id)
    sessionStorage.removeItem("mh.guest-last");
  if (location.hash.startsWith("#access="))
    history.replaceState(null, "", location.pathname + location.search);
}
export function lastOrderUrl() {
  const id = sessionStorage.getItem("mh.guest-last");
  return /^[0-9a-f-]{36}$/i.test(id ?? "") ? orderUrl(id!) : null;
}
export function stableKey(key: string) {
  let value = sessionStorage.getItem(key);
  if (!value) {
    value = crypto.randomUUID();
    sessionStorage.setItem(key, value);
  }
  return value;
}
export const phoneDigits = (value: string) => {
  let digits = value.replace(/\D/g, "");
  if (digits.length === 10 && !/^[78]/.test(digits)) digits = "7" + digits;
  if (digits.startsWith("8")) digits = "7" + digits.slice(1);
  return digits;
};
export const validPhone = (value: string) =>
  /^\+?[\d ()-]+$/.test(value.trim()) && /^7\d{10}$/.test(phoneDigits(value));
export const today = () =>
  new Date().toLocaleDateString("en-CA", { timeZone: "Europe/Moscow" });
export const date = (value: string) =>
  new Intl.DateTimeFormat("ru-RU", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Europe/Moscow",
  }).format(new Date(/[Zz]|[+-]\d\d:\d\d$/.test(value) ? value : value + "Z"));
export const paymentLabels: Record<string, string> = {
  card: "Банковская карта",
  sbp: "СБП",
  yoomoney: "ЮMoney",
  cash: "Наличные при получении",
  terminal: "Картой при получении",
};
export const paymentStatuses: Record<string, string> = {
  Pending: "Ожидает оплаты",
  Paid: "Оплачен",
  Failed: "Ошибка оплаты",
  Cancelled: "Оплата отменена",
  Refunded: "Возвращён",
  PartiallyRefunded: "Частичный возврат",
};
export const fulfillmentLabel = (stage: string, method: string) =>
  (
    ({
      Pending: "Ожидает оплаты",
      Paid: "Оплата получена",
      Failed: "Ошибка оплаты",
      Refund: "Возврат",
      Refunded: "Оплата возвращена",
      PartiallyRefunded: "Частичный возврат",
      New: "Заказ получен",
      Accepted: "Принят",
      Preparing: "Готовится",
      Ready: method === "delivery" ? "Готов к отправке" : "Готов к выдаче",
      OutForDelivery: "У курьера",
      Completed: method === "delivery" ? "Доставлен" : "Выдан",
      Rejected: "Отклонён",
      Cancelled: "Отменён клиентом",
    }) as Record<string, string>
  )[stage] ?? stage;
