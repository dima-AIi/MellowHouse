import { useId, useState, type CSSProperties } from "react";
import { image, imageSet } from "./media";
import { optionHelp, optionLabel, optionPrice } from "./option-labels";
import type {
  Catalog,
  Product,
  Cart,
  Selections,
  PublicSettings,
} from "./types";

export const money = (value: number) =>
  new Intl.NumberFormat("ru-RU", {
    style: "currency",
    currency: "RUB",
    maximumFractionDigits: 0,
  }).format(value);
export function Icon({ name }: { name: string }) {
  return (
    <svg aria-hidden="true">
      <use href={`/images/icons.svg?v=20261028-v20#${name}`} />
    </svg>
  );
}
export interface View {
  cart: Cart;
  favorites: string[];
}
export function ProductCard({
  product: p,
  view,
}: {
  product: Product;
  view: View;
}) {
  const count = view.cart.items
    .filter((line) => line.product.id === p.id)
    .reduce((sum, line) => sum + line.quantity, 0);
  const favorite = view.favorites.includes(p.id);
  return (
    <article className="product-card" data-product={p.id}>
      <a
        className="product-photo"
        href={`/Product/${p.id}`}
        data-product-link=""
      >
        <img
          src={image(p.image)}
          srcSet={imageSet(p.image)}
          sizes="(max-width:700px) 46vw, (max-width:1100px) 30vw, 320px"
          alt={p.name}
          width="640"
          height="426"
          loading="lazy"
        />
      </a>
      <div className="card-copy">
        <h3>
          <a href={`/Product/${p.id}`} data-product-link="">
            {p.name}
          </a>
        </h3>
        <p className="card-description">{p.description}</p>
        <p className="portion">{p.portion}</p>
        <div className="card-actions">
          <strong
            className="card-price"
            aria-label={`Цена ${p.name}: ${money(p.price)} за порцию`}
          >
            {money(p.price)}
          </strong>
          <div className="card-controls">
            <button
              className="icon-btn favorite"
              data-favorite={p.id}
              aria-pressed={favorite}
              aria-label={`${favorite ? "Убрать" : "Добавить"} ${p.name} ${favorite ? "из избранного" : "в избранное"}`}
            >
              <Icon name="heart" />
            </button>
            <div
              className={`add-control${count ? " has-items" : ""}`}
              data-product-stepper={p.id}
            >
              <button
                className="round-btn product-minus"
                data-product-delta="-1"
                data-cart-product={p.id}
                aria-label={`Убрать одну порцию: ${p.name}`}
                hidden={!count}
              >
                −
              </button>
              <output
                className="product-cart-count"
                data-product-count={p.id}
                aria-label={`В корзине: ${p.name}`}
                aria-live="polite"
                hidden={!count}
              >
                {count}
              </output>
              <button
                className="round-btn"
                data-cart-product={p.id}
                data-product-delta="1"
                disabled={!p.available}
                title={p.available ? "Добавить" : "Сейчас недоступно"}
                aria-label={`Добавить ${p.name} в корзину`}
              >
                +
              </button>
            </div>
          </div>
        </div>
      </div>
    </article>
  );
}
export function CatalogSections({
  catalog,
  view,
}: {
  catalog: Catalog;
  view: View;
}) {
  return (
    <>
      {catalog.categories.map((c) => {
        const products = catalog.products.filter((p) => p.categoryId === c.id);
        return (
          <section
            className="catalog-section"
            id={c.id}
            data-section={c.id}
            key={c.id}
          >
            <div className="section-heading">
              <h2>{c.name}</h2>
              <span>
                {products.length ? `${products.length} в меню` : "Скоро"}
              </span>
            </div>
            {products.length ? (
              <div className="product-grid">
                {products.map((p) => (
                  <ProductCard key={p.id} product={p} view={view} />
                ))}
              </div>
            ) : (
              <div className="soon-category">
                <span>Скоро в меню</span>
                <p>
                  Готовим для вас новые вкусы. А пока попробуйте кофе и горячий
                  сэндвич.
                </p>
              </div>
            )}
          </section>
        );
      })}
    </>
  );
}
export function CategoryNav({
  catalog,
  activeId,
}: {
  catalog: Catalog;
  activeId?: string;
}) {
  return (
    <>
      {catalog.categories.map((c) => (
        <a
          key={c.id}
          href={`/Menu#${c.id}`}
          data-category={c.id}
          className={activeId === c.id ? "active" : undefined}
          aria-current={activeId === c.id ? "location" : undefined}
        >
          <span>{c.name}</span>
          <small>
            {catalog.products.filter((p) => p.categoryId === c.id).length}
          </small>
        </a>
      ))}
    </>
  );
}
export function Nutrition({ product: p }: { product: Product }) {
  const facts = p.nutritionFacts;
  if (!facts) return p.nutrition ? <p>{p.nutrition}</p> : null;
  const values: [string, number][] = [
    ["ккал", facts.calories],
    ["Белки, г", facts.protein],
    ["Жиры, г", facts.fat],
    ["Угл., г", facts.carbs],
  ];
  return (
    <div className="nutrition" aria-label="Пищевая ценность на базовую порцию">
      <p className="nutrition-caption">
        КБЖУ на порцию{facts.estimated ? " · ориентировочно" : ""}
      </p>
      <dl className="nutrition-values">
        {values.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>
              {new Intl.NumberFormat("ru-RU", {
                maximumFractionDigits: 1,
              }).format(value)}
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
export function ProductDetail({
  catalog,
  product: p,
  view,
  configuration,
  buy,
  allergenText,
}: {
  catalog: Catalog;
  product: Product;
  view: View;
  configuration?: import("react").ReactNode;
  buy?: import("react").ReactNode;
  allergenText?: string;
}) {
  const category = catalog.categories.find((c) => c.id === p.categoryId);
  const related = catalog.products
    .filter((other) => other.categoryId === p.categoryId && other.id !== p.id)
    .slice(0, 4);
  return (
    <div className="container product-container" data-product-detail={p.id}>
      <nav className="breadcrumbs" aria-label="Хлебные крошки">
        <a href="/Menu" data-return-menu="">
          Меню
        </a>
        <span>/</span>
        <a href={`/Menu#${p.categoryId}`}>{category?.name ?? "Меню"}</a>
        <span>/</span>
        <span>{p.name}</span>
      </nav>
      <div className="product-detail">
        <div className="detail-photo">
          <img
            src={image(p.image, 960)}
            srcSet={imageSet(p.image)}
            sizes="(max-width:700px) 94vw, 50vw"
            alt={p.name}
            width="960"
            height="960"
            fetchPriority="high"
          />
        </div>
        <div className="detail-copy">
          <div className="title-favorite">
            <h1>{p.name}</h1>
            <button
              className="icon-btn favorite"
              data-favorite={p.id}
              aria-label={`Добавить ${p.name} в избранное`}
              aria-pressed={view.favorites.includes(p.id)}
            >
              <Icon name="heart" />
            </button>
          </div>
          <p>{p.description}</p>
          <p className="portion">{p.portion}</p>
          <div
            id="product-options"
            className="configurator"
            aria-busy={configuration ? false : true}
          >
            {configuration}
          </div>
          {buy ?? (
            <div className="product-buy">
              <div className="buy-row">
                <div className="quantity">
                  <button
                    data-detail-qty="-1"
                    aria-label="Уменьшить количество"
                  >
                    −
                  </button>
                  <output id="detail-quantity">1</output>
                  <button data-detail-qty="1" aria-label="Увеличить количество">
                    +
                  </button>
                </div>
                <strong className="large-price" id="detail-price">
                  {money(p.price)}
                </strong>
              </div>
              <button
                className="btn wide"
                id="detail-add"
                disabled={!p.available}
              >
                Добавить в корзину ·{" "}
                <span id="detail-button-price">{money(p.price)}</span>
              </button>
              <output
                className="detail-cart-count"
                data-product-count={p.id}
                data-product-count-label=""
                hidden
              />
            </div>
          )}
          <div className="product-information">
            <details>
              <summary>Состав и ингредиенты</summary>
              <p>{p.ingredients}</p>
              <p id="recipe-changes" />
            </details>
            <details>
              <summary>Аллергены</summary>
              <p id="product-allergens">
                {allergenText ?? p.allergens.join(", ")}
              </p>
              <p>
                На общей кухне возможен перекрёстный контакт. При аллергии
                уточните состав у сотрудника.
              </p>
            </details>
            {(p.nutritionFacts || p.nutrition) && (
              <details className="nutrition-details">
                <summary>КБЖУ и пищевая ценность</summary>
                <div className="accordion-content">
                  <Nutrition product={p} />
                </div>
              </details>
            )}
          </div>
        </div>
      </div>
      <section className="related">
        <div className="section-heading">
          <h2>Вам также может понравиться</h2>
          <a href={`/Menu#${p.categoryId}`}>Вся категория</a>
        </div>
        <div className="product-grid related-grid">
          {related.map((other) => (
            <ProductCard key={other.id} product={other} view={view} />
          ))}
        </div>
      </section>
    </div>
  );
}
export function Faq({ items }: { items: PublicSettings["faq"] }) {
  return (
    <>
      {items.map((item, index) => (
        <details key={`${index}-${item.question}`}>
          <summary>{item.question}</summary>
          <p>{item.answer}</p>
        </details>
      ))}
    </>
  );
}

export function MenuSearch({ catalog }: { catalog: Catalog }) {
  const [query, setQuery] = useState(() => {
    try {
      return sessionStorage.getItem("mh.restore-menu") === "1"
        ? (sessionStorage.getItem("mh.menu-query") ?? "")
        : "";
    } catch {
      return "";
    }
  });
  const words = query
    .trim()
    .toLocaleLowerCase("ru")
    .split(/\s+/)
    .filter(Boolean);
  const found = words.length
    ? catalog.products.filter((p) =>
        words.every((word) =>
          (p.name + " " + p.ingredients).toLocaleLowerCase("ru").includes(word),
        ),
      )
    : [];
  return (
    <>
      <label htmlFor="menu-search">Найдите любимый вкус</label>
      <div className="search-field">
        <input
          id="menu-search"
          type="search"
          placeholder="Название или ингредиент…"
          autoComplete="off"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            try {
              sessionStorage.setItem("mh.menu-query", e.target.value);
            } catch {
              /* Searching still works when storage is unavailable. */
            }
          }}
        />
        <button
          id="menu-search-clear"
          type="button"
          aria-label="Очистить поиск"
          hidden={!query}
          onClick={() => {
            setQuery("");
            try {
              sessionStorage.removeItem("mh.menu-query");
            } catch {}
            document.querySelector<HTMLInputElement>("#menu-search")?.focus();
          }}
        >
          ×
        </button>
      </div>
      <div
        id="search-results"
        className="search-results"
        role="region"
        aria-label="Результаты поиска"
        hidden={!words.length}
      >
        {found.length ? (
          found.map((p) => (
            <a key={p.id} href={`/Product/${p.id}`} data-product-link="">
              <img src={image(p.image)} alt="" width="60" height="60" />
              <span>
                <strong>{p.name}</strong>
                <small>{p.portion}</small>
              </span>
              <b>{money(p.price)}</b>
            </a>
          ))
        ) : (
          <p>
            Попробуйте другое название или ингредиент. Все разделы меню остаются
            ниже.
          </p>
        )}
      </div>
      <p id="search-status" role="status" className="small muted">
        {words.length
          ? found.length
            ? `Найдено: ${found.length}`
            : "Ничего не найдено"
          : ""}
      </p>
    </>
  );
}
export function Configurator({
  product,
  initial,
  onChange,
  onInvalid,
}: {
  product: Product;
  initial: Selections;
  onChange: (selection: Selections) => void;
  onInvalid: (message: string) => void;
}) {
  const [selection, setSelection] = useState<Selections>(() =>
    structuredClone(initial),
  );
  const instance = useId();
  function choose(
    group: Product["groups"][number],
    id: string,
    checked: boolean,
  ) {
    const previous = selection[group.id] ?? [];
    const chosen = group.multiple
      ? checked
        ? [...previous, id]
        : previous.filter((value) => value !== id)
      : id
        ? [id]
        : [];
    const min = Math.max(group.min ?? 0, group.required ? 1 : 0),
      max = group.max ?? (group.multiple ? group.options.length : 1);
    if (chosen.length < min || chosen.length > max) {
      onInvalid(
        chosen.length > max
          ? `В группе «${group.label}» можно выбрать не больше ${max}.`
          : `Выберите вариант в группе «${group.label}».`,
      );
      return;
    }
    const next = { ...selection, [group.id]: chosen };
    setSelection(next);
    onChange(next);
  }
  return (
    <>
      {product.groups.map((group) => {
        const optional =
          !group.multiple &&
          Math.max(group.min ?? 0, group.required ? 1 : 0) === 0;
        return (
          <fieldset key={group.id}>
            <legend>{group.label}</legend>
            {optionHelp(group) && (
              <p className="option-help">{optionHelp(group)}</p>
            )}
            <div
              className={group.multiple ? "option-checks" : "option-grid"}
              data-option-grid={group.id}
              style={
                !group.multiple
                  ? ({
                      "--columns": Math.min(3, group.options.length),
                    } as CSSProperties)
                  : undefined
              }
            >
              {optional && (
                <label className="option-tile">
                  <input
                    type="radio"
                    name={`${instance}-${group.id}`}
                    value=""
                    data-group={group.id}
                    checked={!selection[group.id]?.length}
                    onChange={() => choose(group, "", true)}
                  />
                  <span>Без добавления</span>
                  <small>Без доплаты</small>
                </label>
              )}
              {group.options.map((option) => (
                <label
                  className={group.multiple ? "option-check" : "option-tile"}
                  key={option.id}
                >
                  <input
                    type={group.multiple ? "checkbox" : "radio"}
                    name={`${instance}-${group.id}`}
                    value={option.id}
                    data-group={group.id}
                    checked={(selection[group.id] ?? []).includes(option.id)}
                    onChange={(e) => choose(group, option.id, e.target.checked)}
                  />
                  <span>{optionLabel(group, option)}</span>
                  <small>{optionPrice(product, group, option)}</small>
                </label>
              ))}
            </div>
          </fieldset>
        );
      })}
    </>
  );
}
