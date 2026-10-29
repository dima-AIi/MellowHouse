import { useRef, useState } from "react";
import type { AdminCatalog, AdminCategory, AdminProduct } from "./admin-types";
import { ShopStore } from "./store";
import {
  Modal,
  Field,
  Form,
  Select,
  ListField,
  useAction,
  useResource,
  useUnsavedChanges,
} from "./ui";
import { Configurator, money, Nutrition } from "./components";
import { image } from "./media";
function fresh(categoryId: string): AdminProduct {
  return {
    id: "",
    categoryId,
    name: "",
    price: 0,
    portion: "",
    description: "",
    ingredients: "",
    image: "",
    available: true,
    popular: false,
    recommended: false,
    draft: false,
    archived: false,
    sortOrder: 0,
    cost: null,
    serving: null,
    unit: "г",
    nutrition: "",
    nutritionFacts: null,
    groups: [],
    allergens: [],
  };
}
export function AdminCatalogPage({ store }: { store: ShopStore }) {
  const data = useResource<AdminCatalog>(
      () => store.request("/api/admin/catalog"),
      "catalog",
    ),
    [query, setQuery] = useState(""),
    [category, setCategory] = useState(""),
    [tab, setTab] = useState("active"),
    [page, setPage] = useState(0),
    [product, setProduct] = useState<AdminProduct | null>(null),
    [editingCategory, setEditingCategory] = useState<AdminCategory | null>(
      null,
    ),
    [categoryOpen, setCategoryOpen] = useState(false),
    [archiveCategory, setArchiveCategory] = useState<AdminCategory | null>(
      null,
    ),
    [transfer, setTransfer] = useState(""),
    action = useAction();
  const filtered = (data.value?.products ?? []).filter(
      (p) =>
        (tab === "archived" ? p.archived : !p.archived) &&
        (tab !== "draft" || p.draft) &&
        (tab !== "stop" || (!p.available && !p.draft)) &&
        (!category || p.categoryId === category) &&
        (p.name + " " + p.ingredients)
          .toLowerCase()
          .includes(query.toLowerCase()),
    ),
    pages = Math.max(1, Math.ceil(filtered.length / 24)),
    current = Math.min(page, pages - 1);
  async function toggle(p: AdminProduct, archived: boolean) {
    await action.run(async () => {
      await store.request(
        "/api/admin/products/" + p.id + (archived ? "/restore" : ""),
        {},
        archived ? "POST" : "DELETE",
      );
      await data.reload();
    });
  }
  return (
    <>
      <div className="admin-page-heading">
        <div>
          <h1>Каталог</h1>
          <p>Товары, фотографии и настройки блюд.</p>
        </div>
        <div className="admin-actions">
          <button
            className="btn"
            onClick={() =>
              setProduct(
                fresh(
                  data.value?.categories.find((c) => !c.archived)?.id ?? "",
                ),
              )
            }
          >
            Новый товар
          </button>
          <button
            className="btn btn-outline"
            onClick={() => {
              setEditingCategory(null);
              setCategoryOpen(true);
            }}
          >
            Новая категория
          </button>
        </div>
      </div>
      <div className="admin-tabs">
        {[
          ["active", "Товары"],
          ["draft", "Черновики"],
          ["stop", "Стоп-лист"],
          ["archived", "Архив"],
          ["categories", "Категории"],
        ].map(([value, label]) => (
          <button
            key={value}
            className={tab === value ? "active" : ""}
            onClick={() => {
              setTab(value!);
              setPage(0);
            }}
          >
            {label}
          </button>
        ))}
      </div>
      {data.error && <p className="form-error">{data.error}</p>}
      {action.error && (
        <p className="form-error" role="alert">
          {action.error}
        </p>
      )}
      {tab === "categories" ? (
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Категория</th>
                <th>Порядок</th>
                <th>Видимость</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {data.value?.categories.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td>{c.sortOrder}</td>
                  <td>
                    {c.archived
                      ? "В архиве"
                      : c.visible
                        ? "На сайте"
                        : "Скрыта"}
                  </td>
                  <td>
                    <button
                      className="text-button"
                      onClick={() => {
                        setEditingCategory(c);
                        setCategoryOpen(true);
                      }}
                    >
                      Изменить
                    </button>
                    <button
                      className="text-button"
                      disabled={action.busy}
                      onClick={() =>
                        c.archived
                          ? void action.run(async () => {
                              await store.request(
                                "/api/admin/categories/" + c.id + "/restore",
                                {},
                                "POST",
                              );
                              await data.reload();
                            })
                          : setArchiveCategory(c)
                      }
                    >
                      {c.archived ? "Восстановить" : "В архив"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <>
          <div className="admin-filters">
            <Field
              label="Поиск товара"
              type="search"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setPage(0);
              }}
            />
            <Select
              label="Категория"
              value={category}
              onChange={(value) => {
                setCategory(value);
                setPage(0);
              }}
              options={[
                ["", "Все категории"],
                ...(data.value?.categories ?? []).map(
                  (c) => [c.id, c.name] as [string, string],
                ),
              ]}
            />
            <button
              className="btn btn-outline"
              onClick={() => void data.reload()}
            >
              Обновить
            </button>
          </div>
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Товар</th>
                  <th>Цена</th>
                  <th>Себестоимость</th>
                  <th>Статус</th>
                  <th>Действия</th>
                </tr>
              </thead>
              <tbody>
                {filtered.slice(current * 24, (current + 1) * 24).map((p) => (
                  <tr key={p.id}>
                    <td>
                      <div className="admin-product-name">
                        <img
                          src={image(p.image)}
                          alt=""
                          width="55"
                          height="55"
                        />
                        <strong>{p.name}</strong>
                      </div>
                    </td>
                    <td>{money(p.price)}</td>
                    <td>{p.cost === null ? "Не указана" : money(p.cost)}</td>
                    <td>
                      {p.archived
                        ? "В архиве"
                        : p.draft
                          ? "Черновик"
                          : p.available
                            ? "В меню"
                            : "Стоп-лист"}
                    </td>
                    <td>
                      <button
                        className="text-button"
                        onClick={() => setProduct(structuredClone(p))}
                      >
                        Изменить
                      </button>
                      <button
                        className="text-button"
                        onClick={() =>
                          setProduct({
                            ...structuredClone(p),
                            id: "",
                            name: p.name + " — копия",
                            draft: true,
                            archived: false,
                          })
                        }
                      >
                        Копировать
                      </button>
                      <button
                        className="text-button"
                        disabled={action.busy}
                        onClick={() => void toggle(p, p.archived)}
                      >
                        {p.archived ? "Восстановить" : "В архив"}
                      </button>
                      {!p.archived && (
                        <button
                          className="text-button"
                          disabled={action.busy}
                          onClick={() =>
                            void action.run(async () => {
                              await store.request(
                                "/api/admin/products/" + p.id,
                                { ...p, available: !p.available },
                                "PUT",
                              );
                              await data.reload();
                            })
                          }
                        >
                          {p.available ? "В стоп-лист" : "Вернуть в меню"}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="admin-pagination">
            <span>
              Найдено: {filtered.length} · страница {current + 1} из {pages}
            </span>
            <button
              className="btn btn-outline"
              disabled={!current}
              onClick={() => setPage(current - 1)}
            >
              Назад
            </button>
            <button
              className="btn btn-outline"
              disabled={current + 1 >= pages}
              onClick={() => setPage(current + 1)}
            >
              Далее
            </button>
          </div>
        </>
      )}
      {product && data.value && (
        <ProductEditor
          key={product.id || "new"}
          initial={product}
          categories={data.value.categories}
          store={store}
          onClose={() => setProduct(null)}
          onSaved={() => {
            setProduct(null);
            void data.reload();
          }}
        />
      )}
      {categoryOpen && (
        <CategoryEditor
          initial={editingCategory}
          store={store}
          onClose={() => setCategoryOpen(false)}
          onSaved={() => {
            setCategoryOpen(false);
            void data.reload();
          }}
        />
      )}
      <Modal
        open={!!archiveCategory}
        title="Архивировать категорию?"
        onClose={() => setArchiveCategory(null)}
      >
        <p>
          Товары можно перенести в другую категорию или архивировать вместе с
          категорией.
        </p>
        <Select
          label="Перенести товары"
          value={transfer}
          onChange={setTransfer}
          options={[
            ["", "Архивировать вместе с товарами"],
            ...(data.value?.categories ?? [])
              .filter((c) => !c.archived && c.id !== archiveCategory?.id)
              .map((c) => [c.id, c.name] as [string, string]),
          ]}
        />
        <p className="form-error" role="alert">
          {action.error}
        </p>
        <button
          className="btn"
          disabled={action.busy}
          onClick={() =>
            void action.run(async () => {
              await store.request(
                "/api/admin/categories/" + archiveCategory!.id,
                { transferTo: transfer || null, withProducts: !transfer },
                "DELETE",
              );
              setArchiveCategory(null);
              await data.reload();
            })
          }
        >
          Подтвердить
        </button>
      </Modal>
    </>
  );
}
function ProductEditor({
  initial,
  categories,
  store,
  onClose,
  onSaved,
}: {
  initial: AdminProduct;
  categories: AdminCategory[];
  store: ShopStore;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [p, setP] = useState(initial),
    [uploading, setUploading] = useState(false),
    [preview, setPreview] = useState(false),
    [price, setPrice] = useState(initial.price),
    [discard, setDiscard] = useState(false),
    action = useAction(),
    original = useRef(JSON.stringify(initial)),
    [categoryOpen, setCategoryOpen] = useState(false),
    [extraCategories, setExtraCategories] = useState<AdminCategory[]>([]);
  useUnsavedChanges(JSON.stringify(p) !== original.current);
  function patch<K extends keyof AdminProduct>(key: K, value: AdminProduct[K]) {
    setP((previous) => ({ ...previous, [key]: value }));
  }
  function close() {
    if (JSON.stringify(p) !== original.current) setDiscard(true);
    else onClose();
  }
  async function upload(file: File) {
    await action.run(async () => {
      if (!/\.(png|webp)$/i.test(file.name))
        throw Error("Выберите PNG или WebP с прозрачным фоном.");
      if (file.size > 8 * 1024 * 1024)
        throw Error("Фото должно быть не больше 8 МБ.");
      setUploading(true);
      try {
        const form = new FormData();
        form.set("file", file);
        form.set("kind", "product");
        const reply = await store.request<{
          path: string;
          width: number;
          height: number;
        }>("/api/admin/media", form, "POST");
        patch("image", reply.path);
        store.toast(`Фото готово · ${reply.width} × ${reply.height} px`);
      } finally {
        setUploading(false);
      }
    });
  }
  async function save() {
    await action.run(async () => {
      await store.request(
        "/api/admin/products" + (p.id ? "/" + p.id : ""),
        { ...p, nutrition: p.nutritionFacts ? p.nutrition : "" },
        p.id ? "PUT" : "POST",
      );
      onSaved();
    });
  }
  return (
    <>
      {categoryOpen && (
        <CategoryEditor
          initial={null}
          store={store}
          onClose={() => setCategoryOpen(false)}
          onSaved={async (id) => {
            const catalog =
              await store.request<AdminCatalog>("/api/admin/catalog");
            setExtraCategories(
              catalog.categories.filter(
                (c) => !categories.some((original) => original.id === c.id),
              ),
            );
            if (id) patch("categoryId", id);
            setCategoryOpen(false);
          }}
        />
      )}
      <Modal
        open
        title={p.id ? "Изменить товар" : "Новый товар"}
        onClose={close}
        wide
      >
        <Form
          busy={action.busy || uploading}
          error={action.error}
          onSubmit={() => void save()}
        >
          <div className="admin-product-editor">
            <section>
              <h3>Общая информация</h3>
              <Field
                label="Название товара"
                value={p.name}
                onChange={(e) => patch("name", e.target.value)}
                maxLength={120}
                required
              />
              <Select
                label="Категория"
                value={p.categoryId}
                onChange={(value) => patch("categoryId", value)}
                options={[...categories, ...extraCategories]
                  .filter((c) => !c.archived)
                  .map((c) => [c.id, c.name])}
              />
              <button
                type="button"
                className="text-button"
                onClick={() => setCategoryOpen(true)}
              >
                Создать категорию
              </button>
              <Field
                label="Цена, ₽"
                type="number"
                min={0}
                value={p.price}
                onChange={(e) => patch("price", Number(e.target.value))}
                required
              />
              <Field
                label="Порция"
                value={p.portion}
                onChange={(e) => patch("portion", e.target.value)}
                required={!p.draft}
              />
              <label>
                Описание
                <textarea
                  value={p.description}
                  onChange={(e) => patch("description", e.target.value)}
                  required={!p.draft}
                />
              </label>
              <label>
                Состав
                <textarea
                  value={p.ingredients}
                  onChange={(e) => patch("ingredients", e.target.value)}
                  required={!p.draft}
                />
              </label>
              <ListField
                label="Аллергены через запятую"
                value={p.allergens}
                onChange={(value) => patch("allergens", value)}
              />
              <Field
                label="Себестоимость, ₽"
                type="number"
                min={0}
                value={p.cost ?? ""}
                onChange={(e) =>
                  patch(
                    "cost",
                    e.target.value === "" ? null : Number(e.target.value),
                  )
                }
              />
              <Field
                label="Вес или объём"
                type="number"
                min={0}
                value={p.serving ?? ""}
                onChange={(e) =>
                  patch(
                    "serving",
                    e.target.value === "" ? null : Number(e.target.value),
                  )
                }
              />
              <Select
                label="Единица измерения"
                value={p.unit}
                onChange={(value) => patch("unit", value)}
                options={[
                  ["г", "г"],
                  ["мл", "мл"],
                  ["шт", "шт"],
                ]}
              />
              <Field
                label="Порядок"
                type="number"
                value={p.sortOrder}
                onChange={(e) => patch("sortOrder", Number(e.target.value))}
              />
              {(["available", "popular", "recommended", "draft"] as const).map(
                (key, index) => (
                  <label className="choice" key={key}>
                    <input
                      type="checkbox"
                      checked={p[key]}
                      onChange={(e) => patch(key, e.target.checked)}
                    />
                    {
                      [
                        "Доступен для заказа",
                        "Популярный",
                        "Рекомендуемый",
                        "Черновик",
                      ][index]
                    }
                  </label>
                ),
              )}
              <h3>Пищевая ценность</h3>
              <label className="choice">
                <input
                  type="checkbox"
                  checked={!!p.nutritionFacts}
                  onChange={(e) =>
                    patch(
                      "nutritionFacts",
                      e.target.checked
                        ? {
                            calories: 0,
                            protein: 0,
                            fat: 0,
                            carbs: 0,
                            estimated: true,
                          }
                        : null,
                    )
                  }
                />
                Указать КБЖУ
              </label>
              {p.nutritionFacts && (
                <>
                  {(["calories", "protein", "fat", "carbs"] as const).map(
                    (key, index) => (
                      <Field
                        key={key}
                        label={
                          [
                            "Калории, ккал",
                            "Белки, г",
                            "Жиры, г",
                            "Углеводы, г",
                          ][index]!
                        }
                        type="number"
                        step="0.1"
                        min={0}
                        required
                        value={p.nutritionFacts![key]}
                        onChange={(e) =>
                          patch("nutritionFacts", {
                            ...p.nutritionFacts!,
                            [key]: Number(e.target.value),
                          })
                        }
                      />
                    ),
                  )}
                  <label className="choice">
                    <input
                      type="checkbox"
                      checked={p.nutritionFacts.estimated}
                      onChange={(e) =>
                        patch("nutritionFacts", {
                          ...p.nutritionFacts!,
                          estimated: e.target.checked,
                        })
                      }
                    />
                    Ориентировочные значения
                  </label>
                </>
              )}
            </section>
            <aside>
              <h3>Фотография</h3>
              <div className="admin-photo-preview">
                {p.image ? (
                  <img src={p.image} alt="Фото товара" />
                ) : (
                  <p>PNG или WebP с прозрачным фоном</p>
                )}
              </div>
              <Field
                label="Файл фотографии"
                value={p.image}
                onChange={(e) => patch("image", e.target.value)}
              />
              <label>
                Загрузить PNG или WebP
                <input
                  type="file"
                  accept=".png,.webp,image/png,image/webp"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) void upload(file);
                    e.target.value = "";
                  }}
                />
              </label>
              <p className="small muted">
                Прозрачный фон, до 8 МБ. Размер и прозрачность проверяет сервер.
              </p>
              <button
                className="text-button"
                type="button"
                onClick={() => patch("image", "")}
              >
                Удалить фотографию
              </button>
            </aside>
          </div>
          <h3>Настройки товара</h3>
          {p.groups.map((group, i) => (
            <fieldset className="admin-group" key={group.id}>
              <legend>Группа {i + 1}</legend>
              <Field
                label="Название группы"
                value={group.label}
                required
                onChange={(e) =>
                  patch(
                    "groups",
                    p.groups.map((g, index) =>
                      index === i ? { ...g, label: e.target.value } : g,
                    ),
                  )
                }
              />
              <label className="choice">
                <input
                  type="checkbox"
                  checked={group.multiple}
                  onChange={(e) =>
                    patch(
                      "groups",
                      p.groups.map((g, index) =>
                        index === i
                          ? {
                              ...g,
                              multiple: e.target.checked,
                              defaults: e.target.checked
                                ? g.defaults
                                : g.defaults.slice(0, 1),
                            }
                          : g,
                      ),
                    )
                  }
                />
                Несколько вариантов
              </label>
              <label className="choice">
                <input
                  type="checkbox"
                  checked={group.required}
                  onChange={(e) =>
                    patch(
                      "groups",
                      p.groups.map((g, index) =>
                        index === i ? { ...g, required: e.target.checked } : g,
                      ),
                    )
                  }
                />
                Обязательная
              </label>
              {(["min", "max"] as const).map((key) => (
                <Field
                  key={key}
                  label={key === "min" ? "Минимум" : "Максимум"}
                  type="number"
                  min={0}
                  max={30}
                  value={group[key] ?? ""}
                  onChange={(e) =>
                    patch(
                      "groups",
                      p.groups.map((g, index) =>
                        index === i
                          ? {
                              ...g,
                              [key]:
                                e.target.value === ""
                                  ? null
                                  : Number(e.target.value),
                            }
                          : g,
                      ),
                    )
                  }
                />
              ))}
              {group.options.map((option, j) => (
                <div className="admin-option" key={option.id}>
                  {(["label", "price", "cost"] as const).map((key) => (
                    <Field
                      key={key}
                      label={
                        key === "label"
                          ? "Вариант"
                          : key === "price"
                            ? "Надбавка, ₽"
                            : "Себестоимость, ₽"
                      }
                      type={key === "label" ? "text" : "number"}
                      value={option[key] ?? ""}
                      onChange={(e) =>
                        patch(
                          "groups",
                          p.groups.map((g, index) =>
                            index === i
                              ? {
                                  ...g,
                                  options: g.options.map((o, k) =>
                                    k === j
                                      ? {
                                          ...o,
                                          [key]:
                                            key === "label"
                                              ? e.target.value
                                              : key === "cost" &&
                                                  e.target.value === ""
                                                ? null
                                                : Number(e.target.value),
                                        }
                                      : o,
                                  ),
                                }
                              : g,
                          ),
                        )
                      }
                    />
                  ))}
                  {(["addAllergens", "removeAllergens"] as const).map((key) => (
                    <ListField
                      key={key}
                      label={
                        key === "addAllergens"
                          ? "Добавляет аллергены"
                          : "Исключает аллергены"
                      }
                      value={option[key] ?? []}
                      onChange={(value) =>
                        patch(
                          "groups",
                          p.groups.map((g, index) =>
                            index === i
                              ? {
                                  ...g,
                                  options: g.options.map((o, k) =>
                                    k === j ? { ...o, [key]: value } : o,
                                  ),
                                }
                              : g,
                          ),
                        )
                      }
                    />
                  ))}
                  <label className="choice">
                    <input
                      type="checkbox"
                      checked={group.defaults.includes(option.id)}
                      onChange={(e) =>
                        patch(
                          "groups",
                          p.groups.map((g, index) =>
                            index === i
                              ? {
                                  ...g,
                                  defaults: e.target.checked
                                    ? g.multiple
                                      ? [...g.defaults, option.id]
                                      : [option.id]
                                    : g.defaults.filter(
                                        (id) => id !== option.id,
                                      ),
                                }
                              : g,
                          ),
                        )
                      }
                    />
                    По умолчанию
                  </label>
                  <button
                    type="button"
                    className="danger-link"
                    onClick={() =>
                      patch(
                        "groups",
                        p.groups.map((g, index) =>
                          index === i
                            ? {
                                ...g,
                                options: g.options.filter(
                                  (o) => o.id !== option.id,
                                ),
                                defaults: g.defaults.filter(
                                  (id) => id !== option.id,
                                ),
                              }
                            : g,
                        ),
                      )
                    }
                  >
                    Удалить вариант
                  </button>
                </div>
              ))}
              <button
                type="button"
                className="text-button"
                onClick={() =>
                  patch(
                    "groups",
                    p.groups.map((g, index) =>
                      index === i
                        ? {
                            ...g,
                            options: [
                              ...g.options,
                              {
                                id: "o-" + crypto.randomUUID().slice(0, 8),
                                label: "",
                                price: 0,
                                cost: null,
                              },
                            ],
                          }
                        : g,
                    ),
                  )
                }
              >
                Добавить вариант
              </button>
              <button
                type="button"
                className="danger-link"
                onClick={() =>
                  patch(
                    "groups",
                    p.groups.filter((_, index) => index !== i),
                  )
                }
              >
                Удалить группу
              </button>
            </fieldset>
          ))}
          <button
            type="button"
            className="text-button"
            onClick={() =>
              patch("groups", [
                ...p.groups,
                {
                  id: "g-" + crypto.randomUUID().slice(0, 8),
                  label: "",
                  multiple: false,
                  required: false,
                  min: null,
                  max: null,
                  defaults: [],
                  options: [
                    {
                      id: "o-" + crypto.randomUUID().slice(0, 8),
                      label: "",
                      price: 0,
                    },
                  ],
                },
              ])
            }
          >
            Добавить группу
          </button>
          <div className="dialog-actions">
            <button
              type="button"
              className="btn btn-outline"
              onClick={() => {
                setPreview(true);
                setPrice(p.price);
              }}
            >
              Предпросмотр
            </button>
            <button className="btn" type="submit">
              Сохранить товар
            </button>
          </div>
        </Form>
      </Modal>
      <Modal
        open={preview}
        title="Предпросмотр товара"
        onClose={() => setPreview(false)}
      >
        <div className="preview-product">
          <img src={p.image || "/images/pwa-192.png"} alt={p.name} />
          <h3>{p.name}</h3>
          <p>{p.description}</p>
          <p>{p.portion}</p>
          <p>{p.ingredients}</p>
          <div className="configurator">
            <Configurator
              key={JSON.stringify(p.groups)}
              product={p}
              initial={store.defaults(p)}
              onInvalid={store.toast}
              onChange={(selection) =>
                setPrice(store.calculated(p, selection).price)
              }
            />
          </div>
          <strong className="large-price">{money(price)}</strong>
          <Nutrition product={p} />
        </div>
      </Modal>
      <Modal
        open={discard}
        title="Отменить изменения?"
        onClose={() => setDiscard(false)}
      >
        <p>В форме есть несохранённые изменения.</p>
        <div className="dialog-actions">
          <button className="btn btn-outline" onClick={() => setDiscard(false)}>
            Продолжить редактирование
          </button>
          <button className="btn" onClick={onClose}>
            Закрыть без сохранения
          </button>
        </div>
      </Modal>
    </>
  );
}
function CategoryEditor({
  initial,
  store,
  onClose,
  onSaved,
}: {
  initial: AdminCategory | null;
  store: ShopStore;
  onClose: () => void;
  onSaved: (id?: string) => void;
}) {
  const [data, setData] = useState(
      initial ?? {
        id: "",
        name: "",
        sortOrder: 0,
        visible: true,
        archived: false,
        image: "",
        onHome: false,
      },
    ),
    action = useAction(),
    original = useRef(JSON.stringify(data));
  const dirty = JSON.stringify(data) !== original.current;
  useUnsavedChanges(dirty);
  return (
    <Modal
      open
      title={initial ? "Изменить категорию" : "Новая категория"}
      onClose={() => {
        if (!dirty || confirm("Закрыть категорию без сохранения изменений?"))
          onClose();
      }}
    >
      <Form
        busy={action.busy}
        error={action.error}
        onSubmit={() =>
          void action.run(async () => {
            const result = await store.request<{ id: string }>(
              "/api/admin/categories" + (data.id ? "/" + data.id : ""),
              data,
              data.id ? "PUT" : "POST",
            );
            onSaved(result.id);
          })
        }
      >
        <Field
          label="Название"
          value={data.name}
          required
          onChange={(e) => setData({ ...data, name: e.target.value })}
        />
        <Field
          label="Порядок"
          type="number"
          value={data.sortOrder}
          onChange={(e) =>
            setData({ ...data, sortOrder: Number(e.target.value) })
          }
        />
        <Field
          label="Изображение"
          value={data.image}
          onChange={(e) => setData({ ...data, image: e.target.value })}
        />
        <label>
          Загрузить изображение
          <input
            type="file"
            accept="image/png,image/webp,image/jpeg"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file)
                void action.run(async () => {
                  const form = new FormData();
                  form.set("file", file);
                  form.set("kind", "category");
                  const result = await store.request<{ path: string }>(
                    "/api/admin/media",
                    form,
                    "POST",
                  );
                  setData((previous) => ({ ...previous, image: result.path }));
                });
            }}
          />
        </label>
        <label className="choice">
          <input
            type="checkbox"
            checked={data.visible}
            onChange={(e) => setData({ ...data, visible: e.target.checked })}
          />
          Видима на сайте
        </label>
        <label className="choice">
          <input
            type="checkbox"
            checked={data.onHome}
            onChange={(e) => setData({ ...data, onHome: e.target.checked })}
          />
          Показывать на главной
        </label>
        <button className="btn" type="submit">
          Сохранить категорию
        </button>
      </Form>
    </Modal>
  );
}
