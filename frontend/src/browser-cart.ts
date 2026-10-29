import type { Cart, Product, Selections } from "./types";
export const browserCartKey = "mh.browser-cart.v17";
interface DraftLine {
  id: string;
  productId: string;
  name: string;
  selections: Selections;
  canonical: string;
  expectedUnitPrice: number;
  quantity: number;
}
interface Draft {
  id: string;
  frozen: boolean;
  lines: DraftLine[];
}
export function configureLocal(product: Product, input?: Selections) {
  if (!product.available) throw Error("Эта позиция сейчас недоступна.");
  const selections: Selections = {},
    summary: string[] = [];
  let price = product.price;
  for (const group of product.groups) {
    const ids = [...new Set(input?.[group.id] ?? group.defaults)].sort();
    if (
      (group.required && !ids.length) ||
      (!group.multiple && ids.length > 1) ||
      ids.length < (group.min ?? 0) ||
      ids.length > (group.max ?? Infinity)
    )
      throw Error("Проверьте настройки: " + group.label);
    selections[group.id] = ids;
    for (const id of ids) {
      const option = group.options.find((o) => o.id === id);
      if (!option) throw Error("Недоступная настройка: " + group.label);
      price += option.price;
      summary.push(option.label);
    }
  }
  if (!Number.isSafeInteger(price) || price < 0)
    throw Error("Не удалось рассчитать цену.");
  return {
    selections,
    summary,
    price,
    canonical: JSON.stringify(
      Object.fromEntries(
        Object.entries(selections).sort(([a], [b]) => a.localeCompare(b)),
      ),
    ),
  };
}
export class BrowserCart {
  constructor(
    private storage: Storage,
    private changed: () => void,
  ) {}
  read(): Draft | null {
    try {
      const value = JSON.parse(
        this.storage.getItem(browserCartKey) ?? "null",
      ) as Draft;
      if (
        !value ||
        typeof value.id !== "string" ||
        !Array.isArray(value.lines) ||
        value.lines.length > 50 ||
        value.lines.some(
          (l) =>
            typeof l.id !== "string" ||
            typeof l.productId !== "string" ||
            !Number.isInteger(l.quantity) ||
            l.quantity < 1 ||
            l.quantity > 99 ||
            !l.selections ||
            typeof l.selections !== "object",
        )
      )
        return null;
      return value;
    } catch {
      return null;
    }
  }
  save(value: Draft) {
    try {
      this.storage.setItem(browserCartKey, JSON.stringify(value));
    } catch {
      throw Error(
        "Браузер не смог сохранить корзину. Разрешите хранение данных сайта.",
      );
    }
    this.changed();
  }
  editable() {
    const value = this.read() ?? {
      id: crypto.randomUUID(),
      lines: [],
      frozen: false,
    };
    if (value.frozen)
      throw Error(
        "Сверяем сохранённую корзину с кафе. Подождите подключения или нажмите «Повторить».",
      );
    return value;
  }
  add(product: Product, selections?: Selections, quantity = 1) {
    this.quantity(quantity);
    const value = this.editable(),
      config = configureLocal(product, selections),
      line = value.lines.find(
        (l) => l.productId === product.id && l.canonical === config.canonical,
      );
    if (line) {
      this.quantity(line.quantity + quantity);
      line.quantity += quantity;
    } else {
      if (value.lines.length >= 50)
        throw Error("В корзине может быть до 50 вариантов блюд.");
      value.lines.push({
        id: crypto.randomUUID(),
        productId: product.id,
        name: product.name,
        selections: config.selections,
        canonical: config.canonical,
        expectedUnitPrice: config.price,
        quantity,
      });
    }
    this.save(value);
  }
  edit(id: string, product: Product, selections: Selections, quantity: number) {
    this.quantity(quantity);
    const value = this.editable(),
      line = value.lines.find((l) => l.id === id);
    if (!line) throw Error("Позиция изменилась. Откройте корзину заново.");
    const config = configureLocal(product, selections),
      same = value.lines.find(
        (l) =>
          l.id !== id &&
          l.productId === product.id &&
          l.canonical === config.canonical,
      );
    if (same) {
      this.quantity(same.quantity + quantity);
      same.quantity += quantity;
      value.lines = value.lines.filter((l) => l.id !== id);
    } else
      Object.assign(line, {
        selections: config.selections,
        canonical: config.canonical,
        expectedUnitPrice: config.price,
        quantity,
      });
    this.save(value);
  }
  remove(id?: string) {
    const value = this.editable();
    value.lines = id ? value.lines.filter((l) => l.id !== id) : [];
    this.save(value);
  }
  freeze() {
    const value = this.read();
    if (!value?.lines.length) return null;
    if (!value.frozen) {
      value.frozen = true;
      this.save(value);
    }
    return value;
  }
  acknowledge(id: string) {
    if (this.read()?.id === id) {
      this.storage.removeItem(browserCartKey);
      this.changed();
    }
  }
  private quantity(value: number) {
    if (!Number.isInteger(value) || value < 1 || value > 99)
      throw Error("Количество — от 1 до 99.");
  }
  view(products: Product[] = []): Cart {
    const items = (this.read()?.lines ?? []).map((line) => {
      const product = products.find((p) => p.id === line.productId) ?? {
        id: line.productId,
        categoryId: "",
        name: line.name,
        image: "/images/pwa-192.png",
        portion: "",
        groups: [],
        allergens: [],
        available: false,
        price: line.expectedUnitPrice,
        description: "",
        ingredients: "",
        popular: false,
        recommended: false,
      };
      let config;
      try {
        config = configureLocal(product, line.selections);
      } catch {
        config = {
          price: line.expectedUnitPrice ?? 0,
          summary: ["Будет проверено после подключения"],
        };
      }
      return {
        id: line.id,
        product,
        selections: line.selections,
        summary: config.summary,
        quantity: line.quantity,
        unitPrice: config.price,
        total: config.price * line.quantity,
      };
    });
    return {
      items,
      subtotal: items.reduce((sum, l) => sum + l.total, 0),
      count: items.reduce((sum, l) => sum + l.quantity, 0),
      revision: 0,
    };
  }
}
