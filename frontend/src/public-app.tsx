import { useEffect, useMemo, useRef, useState, type MouseEvent } from "react";
import { ShopStore, useShop } from "./store";
import type { Catalog, PublicSettings, Selections } from "./types";
import { Header, Footer } from "./chrome";
import {
  CatalogSections,
  CategoryNav,
  Configurator,
  Faq,
  Icon,
  MenuSearch,
  Nutrition,
  ProductCard,
  ProductDetail,
  money,
} from "./components";
import { CartDialog, Auth, Method } from "./cart-auth";
import { Checkout } from "./checkout";
import { Account, ResetPassword } from "./account";
import { OrderPage } from "./orders";
import { Assistant } from "./assistant";
import { Empty, useAction } from "./ui";
const reduced = () => matchMedia("(prefers-reduced-motion:reduce)").matches;
function StaticContent({
  html,
  settings,
}: {
  html: string;
  settings?: PublicSettings;
}) {
  const content = useMemo(() => {
    const template = document.createElement("template");
    template.innerHTML = html;
    template.content
      .querySelectorAll('script,input[name="__RequestVerificationToken"]')
      .forEach((el) => el.remove());
    if (settings) {
      const values: Record<string, string> = {
        "delivery-price": money(settings.deliveryPrice ?? 190),
        "delivery-cities": settings.deliveryCities?.join(", ") ?? "Москва",
        hours: settings.hours ?? "",
        "cafe-name": settings.cafeName ?? "Mellow House",
        address: settings.address ?? "Адрес уточняется",
        "payment-methods": (settings.paymentMethods ?? [])
          .map(
            (value) =>
              (
                ({
                  card: "Банковская карта",
                  sbp: "СБП",
                  yoomoney: "ЮMoney",
                  cash: "Наличные при получении",
                  terminal: "Картой при получении",
                }) as Record<string, string>
              )[value] ?? value,
          )
          .join(", "),
      };
      template.content
        .querySelectorAll<HTMLElement>("[data-site-field]")
        .forEach((el) => {
          if (
            el.dataset.siteField &&
            values[el.dataset.siteField] !== undefined
          )
            el.textContent = values[el.dataset.siteField]!;
        });
      const rows = template.content.querySelectorAll(
        ".contact-list .contact-row>div",
      );
      if (rows.length === 4) {
        const contacts = [
          settings.phone || "Телефон скоро появится",
          settings.email || "Контактная почта уточняется",
          settings.address || "Адрес кафе уточняется",
          settings.hours || "График уточняется",
        ];
        rows.forEach((row, index) => {
          const strong = row.querySelector("strong");
          if (strong) strong.textContent = contacts[index]!;
          const link = row.querySelector("a");
          if (link && index < 2)
            link.setAttribute(
              "href",
              index === 0
                ? "tel:" + settings.phone
                : "mailto:" + settings.email,
            );
        });
      }
    }
    return template.innerHTML;
  }, [html, settings]);
  return (
    <div
      className="static-content"
      dangerouslySetInnerHTML={{ __html: content }}
    />
  );
}
function Welcome() {
  const [shown, setShown] = useState(() => {
    try {
      const seen = sessionStorage.getItem("mh.welcome.v4") === "1";
      sessionStorage.setItem("mh.welcome.v4", "1");
      return !seen && !reduced() && !location.hash;
    } catch {
      return false;
    }
  });
  useEffect(() => {
    document.documentElement.classList.add("mh-welcome-ready");
    const timer = setTimeout(() => setShown(false), 2500),
      escape = (event: KeyboardEvent) => {
        if (event.key === "Escape") setShown(false);
      };
    document.addEventListener("keydown", escape);
    return () => {
      clearTimeout(timer);
      document.removeEventListener("keydown", escape);
    };
  }, []);
  return shown ? (
    <div
      className="brand-intro"
      role="region"
      aria-label="Добро пожаловать в Mellow House"
      onClick={() => setShown(false)}
    >
      <div className="brand-intro-content">
        <img
          src="/images/mellow-emblem-v4.svg"
          alt="Фирменный знак Mellow House"
          width="120"
          height="120"
        />
        <strong>Mellow House</strong>
        <p>Хороший вкус. Тёплые встречи.</p>
      </div>
    </div>
  ) : null;
}
function Home({
  store,
  catalog,
  html,
}: {
  store: ShopStore;
  catalog: Catalog;
  html: string;
}) {
  const settings = store.app.state?.settings ?? store.publicSettings,
    view = {
      cart: store.cartView(),
      favorites: store.app.state?.favorites ?? [],
    },
    track = useRef<HTMLDivElement>(null),
    [position, setPosition] = useState({ prev: false, next: true });
  const promos = useMemo(() => {
    const doc = new DOMParser().parseFromString(html, "text/html");
    return doc.querySelector(".home-promos")?.innerHTML ?? "";
  }, [html]);
  const reveal = useRef<HTMLElement>(null);
  useEffect(() => {
    const host = reveal.current;
    if (!host) return;
    const parts = host.querySelectorAll("h2,p");
    if (
      reduced() ||
      document.documentElement.classList.contains("mh-welcome-return")
    ) {
      parts.forEach((el) => el.classList.add("is-in-view"));
      host.classList.add("visible");
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) =>
          entry.target.classList.toggle("is-in-view", entry.isIntersecting),
        );
        host.classList.toggle(
          "visible",
          [...parts].some((el) => el.classList.contains("is-in-view")),
        );
      },
      { threshold: 0.12, rootMargin: "0px 0px -8% 0px" },
    );
    parts.forEach((el) => observer.observe(el));
    return () => observer.disconnect();
  }, []);
  return (
    <>
      <Welcome />
      <section className="home-intro">
        <div className="hero">
          <picture>
            <source
              media="(max-width:600px)"
              srcSet="/images/hero-mobile-v2.webp"
            />
            <img
              className="hero-image"
              src="/images/hero-v2.webp"
              alt="Кофе, горячий сэндвич, свежий салат и говяжьи медальоны на столе Mellow House"
              width="1536"
              height="1024"
              fetchPriority="high"
            />
          </picture>
          <div className="hero-shade" />
          <div className="hero-welcome">
            <h1>
              Добро пожаловать
              <br />в Mellow House
            </h1>
            <p>Кофе, любимая еда и время для себя</p>
            <a className="btn btn-white" href="/Menu">
              Посмотреть меню
            </a>
          </div>
        </div>
        <div className="container intro-content">
          <section
            className="reveal-message"
            aria-label="Заказывайте заранее"
            ref={reveal}
          >
            <h2>
              <span className="reveal-line">Заказывайте заранее.</span>
              <span className="reveal-line">Забирайте без ожидания.</span>
              <span className="reveal-line">Наслаждайтесь Mellow House.</span>
            </h2>
            <p>
              Выберите кофе, завтрак или любимое блюдо — мы приготовим к вашему
              приходу.
            </p>
          </section>
          <section className="popular">
            <div className="section-heading">
              <h2>Самые популярные товары</h2>
              <div className="carousel-controls">
                <button
                  className="outline-round"
                  disabled={!position.prev}
                  aria-label="Предыдущие популярные товары"
                  onClick={() =>
                    track.current?.scrollBy({
                      left: -track.current.clientWidth * 0.7,
                      behavior: reduced() ? "instant" : "smooth",
                    })
                  }
                >
                  ‹
                </button>
                <button
                  className="outline-round"
                  disabled={!position.next}
                  aria-label="Следующие популярные товары"
                  onClick={() =>
                    track.current?.scrollBy({
                      left: track.current.clientWidth * 0.7,
                      behavior: reduced() ? "instant" : "smooth",
                    })
                  }
                >
                  ›
                </button>
              </div>
            </div>
            <div
              className="popular-track"
              id="popular-track"
              ref={track}
              onScroll={() => {
                const el = track.current!;
                setPosition({
                  prev: el.scrollLeft > 1,
                  next: el.scrollLeft < el.scrollWidth - el.clientWidth - 2,
                });
              }}
            >
              {catalog.products
                .filter((p) => p.popular && p.available)
                .map((p) => (
                  <ProductCard key={p.id} product={p} view={view} />
                ))}
            </div>
          </section>
        </div>
      </section>
      <section className="container category-section">
        <h2>Наше меню еды и напитков</h2>
        <div className="photo-categories">
          {catalog.categories
            .filter((c) => c.onHome && c.image)
            .map((c) => (
              <a className="photo-category" key={c.id} href={"/Menu#" + c.id}>
                <img
                  src={c.image}
                  alt=""
                  width="640"
                  height="426"
                  loading="lazy"
                />
                <h3>{c.name}</h3>
              </a>
            ))}
        </div>
      </section>
      <div className="container home-promos">
        <StaticContent html={promos} settings={settings} />
      </div>
      <section className="container value-section">
        <h2>{settings?.marketingTitle ?? "Ваш кофе. Ваша пауза."}</h2>
        <div className="value-grid">
          {[
            [
              "cup",
              "Кофе под ваш вкус",
              "Ваш объём, любимое молоко и ровно столько сладости, сколько хочется.",
            ],
            [
              "heart",
              "Любимая еда рядом",
              "Горячий сэндвич и чашка кофе — для приятной паузы среди большого дня.",
            ],
            [
              "clock",
              "В вашем ритме",
              "Соберите заказ заранее и выберите удобное время. Больше времени для себя.",
            ],
          ].map(([icon, title, text]) => (
            <article key={icon}>
              <Icon name={icon!} />
              <h3>{title}</h3>
              <p>{text}</p>
            </article>
          ))}
        </div>
      </section>
      <section className="container assistant-section" id="assistant-section">
        <div className="faq-column">
          <h2>
            Есть вопрос?
            <br />
            Давайте разберёмся.
          </h2>
          <p>Про состав, любимый кофе и удобный способ получить заказ.</p>
          <div className="faq-list">
            <Faq items={settings?.faq ?? []} />
          </div>
          <p className="faq-help">
            Не нашли ответ? Напишите помощнику — он подскажет по меню и вашим
            заказам.
          </p>
        </div>
        <div className="assistant-card">
          <Assistant runtime={store} catalog={catalog} />
        </div>
      </section>
    </>
  );
}
function Menu({ store, catalog }: { store: ShopStore; catalog: Catalog }) {
  const settings = store.app.state?.settings ?? store.publicSettings,
    view = {
      cart: store.cartView(),
      favorites: store.app.state?.favorites ?? [],
    },
    tab = new URLSearchParams(location.search).get("tab") ?? "menu",
    video = useRef<HTMLVideoElement>(null),
    [paused, setPaused] = useState(reduced()),
    [active, setActive] = useState(catalog.categories[0]?.id ?? "");
  useEffect(() => {
    const element = video.current;
    if (!element) return;
    if (reduced()) {
      element.pause();
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && !paused && !document.hidden)
          void element.play().catch(() => {});
        else element.pause();
      },
      { threshold: 0.15 },
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, [paused]);
  useEffect(() => {
    const elements = document.querySelectorAll<HTMLElement>("[data-section]");
    const update = () => {
      let chosen = elements[0]?.id ?? "";
      for (const el of elements) {
        if (el.getBoundingClientRect().top < 220) chosen = el.id;
        else break;
      }
      setActive(chosen);
    };
    window.addEventListener("scroll", update, { passive: true });
    update();
    return () => window.removeEventListener("scroll", update);
  }, [catalog, tab]);
  const toggleVideo = () => {
    if (video.current?.paused) {
      setPaused(false);
      void video.current.play().catch(() => {});
    } else {
      setPaused(true);
      video.current?.pause();
    }
  };
  const special = catalog.products.filter((p) =>
    tab === "favorites" ? view.favorites.includes(p.id) : p.recommended,
  );
  return (
    <>
      <section className="menu-intro">
        <div className="menu-intro-copy">
          <h1>Наше меню</h1>
          <h2>{settings?.menuTitle ?? "Найдите свой любимый вкус"}</h2>
          <p>
            {settings?.menuText ??
              "Кофе по вашему вкусу, горячие сэндвичи и время для себя."}
          </p>
          <a className="btn" href="#catalog-start">
            Выбрать что-нибудь вкусное
          </a>
          <a className="text-link" href="/Allergens">
            Состав и аллергены
          </a>
        </div>
        <div className="menu-video">
          <video
            ref={video}
            id="menu-video"
            autoPlay={!reduced()}
            muted
            loop
            playsInline
            preload="metadata"
            poster={settings?.poster ?? "/images/coffee-final-v4.webp"}
            tabIndex={0}
            aria-label="Приготовление кофе. Нажмите для воспроизведения или паузы."
            onClick={toggleVideo}
            onKeyDown={(e) => {
              if (e.key === " " || e.key === "Enter") {
                e.preventDefault();
                toggleVideo();
              }
            }}
          >
            <source
              src={settings?.video ?? "/videos/coffee-full-v5.mp4"}
              type="video/mp4"
            />
          </video>
          <span className="video-caption">
            Маленький ритуал.
            <br />
            Большое удовольствие.
          </span>
          <span className="video-help">
            Пауза — нажмите на видео или пробел
          </span>
        </div>
      </section>
      <section className="container allergen-note">
        <div>
          <strong>Знайте, что в вашей чашке и на тарелке</strong>
          <p>
            Состав и аллергены указаны в карточках. На общем оборудовании
            возможны их следы.
          </p>
        </div>
      </section>
      <div className="container catalog-search" id="catalog-start">
        <MenuSearch catalog={catalog} />
      </div>
      <div className="container menu-layout" data-menu-tab={tab}>
        <aside
          className="category-nav"
          aria-label="Категории меню"
          data-active-category={active}
        >
          <CategoryNav catalog={catalog} activeId={active} />
        </aside>
        <div className="catalog-main">
          <div className="catalog-toolbar">
            <nav className="menu-tabs" aria-label="Разделы меню">
              <a href="/Menu" className={tab === "menu" ? "active" : ""}>
                Меню
              </a>
              <a
                href="/Menu?tab=recommended#catalog-start"
                className={tab === "recommended" ? "active" : ""}
              >
                Рекомендуемое
              </a>
              <a href="/Account">История заказов</a>
              <a
                href="/Menu?tab=favorites#catalog-start"
                className={tab === "favorites" ? "active" : ""}
              >
                Избранное
              </a>
            </nav>
            <div className="fulfilment">
              <Method store={store} />
            </div>
          </div>
          {tab === "favorites" || tab === "recommended" ? (
            <section id="special-products">
              <h2>{tab === "favorites" ? "Избранное" : "Рекомендуемое"}</h2>
              <div className="product-grid">
                {special.map((p) => (
                  <ProductCard key={p.id} product={p} view={view} />
                ))}
              </div>
              {!special.length && (
                <Empty
                  title={
                    tab === "favorites"
                      ? "Здесь будут ваши любимые блюда"
                      : "Пока нет рекомендаций"
                  }
                  text="Выберите что-нибудь вкусное в меню."
                >
                  <a className="btn" href="/Menu">
                    Открыть меню
                  </a>
                </Empty>
              )}
            </section>
          ) : (
            <div id="catalog-sections">
              <CatalogSections catalog={catalog} view={view} />
            </div>
          )}
        </div>
      </div>
      <button className="mobile-cart-bar" data-open-cart="">
        <Icon name="bag" />
        Корзина
        {store.phase === "connected" || view.cart.count > 0
          ? " · " + money(view.cart.subtotal)
          : ""}
        <span>›</span>
      </button>
    </>
  );
}
function ProductPage({
  store,
  catalog,
}: {
  store: ShopStore;
  catalog: Catalog;
}) {
  let id = "";
  try {
    id = decodeURIComponent(
      location.pathname.split("/").filter(Boolean).at(-1) ?? "",
    );
  } catch {}
  const product = catalog.products.find((p) => p.id === id),
    [quantity, setQuantity] = useState(1),
    [selection, setSelection] = useState<Selections>(() =>
      product ? store.defaults(product) : {},
    ),
    action = useAction();
  if (!product)
    return (
      <Empty
        title="Товар не найден"
        text="Возможно, он больше не входит в меню."
      >
        <a className="btn" href="/Menu">
          Открыть меню
        </a>
      </Empty>
    );
  const price = store.calculated(product, selection),
    count = store
      .cartView()
      .items.filter((item) => item.product.id === id)
      .reduce((sum, item) => sum + item.quantity, 0);
  return (
    <>
      <ProductDetail
        catalog={catalog}
        product={product}
        view={{
          cart: store.cartView(),
          favorites: store.app.state?.favorites ?? [],
        }}
        configuration={
          <Configurator
            product={product}
            initial={selection}
            onChange={setSelection}
            onInvalid={store.toast}
          />
        }
        allergenText={
          price.allergens.length
            ? "По выбранной рецептуре: " + price.allergens.join(", ")
            : "Отмеченных аллергенов в составе нет. Это не гарантия отсутствия следов."
        }
        buy={
          <div className="product-buy">
            <div className="buy-row">
              <div className="quantity">
                <button
                  aria-label="Уменьшить количество"
                  onClick={() => setQuantity(Math.max(1, quantity - 1))}
                >
                  −
                </button>
                <output>{quantity}</output>
                <button
                  aria-label="Увеличить количество"
                  onClick={() => setQuantity(Math.min(99, quantity + 1))}
                >
                  +
                </button>
              </div>
              <strong className="large-price">
                {money(price.price * quantity)}
              </strong>
            </div>
            <button
              className="btn wide"
              disabled={action.busy || !product.available}
              onClick={() =>
                void action.run(async () => {
                  await store.add(product, selection, quantity);
                  store.toast(product.name + " — в корзине");
                })
              }
            >
              Добавить в корзину · {money(price.price * quantity)}
            </button>
            {count > 0 && (
              <output className="detail-cart-count">
                В корзине: {count} шт.
              </output>
            )}
            <p className="form-error" role="alert">
              {action.error}
            </p>
          </div>
        }
      />
    </>
  );
}
function MobileMenu({ open, close }: { open: boolean; close: () => void }) {
  const ref = useRef<HTMLDialogElement>(null),
    [visible, setVisible] = useState(false);
  useEffect(() => {
    const dialog = ref.current;
    if (open) {
      dialog?.showModal();
      requestAnimationFrame(() =>
        requestAnimationFrame(() => setVisible(true)),
      );
      return;
    }
    setVisible(false);
    const timer = setTimeout(() => dialog?.close(), reduced() ? 0 : 550);
    return () => clearTimeout(timer);
  }, [open]);
  return (
    <dialog
      ref={ref}
      id="mobile-nav"
      className={"mobile-menu" + (visible ? " is-visible" : "")}
      aria-labelledby="mobile-menu-title"
      onCancel={(e) => {
        e.preventDefault();
        close();
      }}
    >
      <div className="mobile-menu-heading">
        <div>
          <p>Mellow House</p>
          <h2 id="mobile-menu-title">Всё, что вам по вкусу</h2>
        </div>
        <button className="icon-btn" aria-label="Закрыть меню" onClick={close}>
          <Icon name="x" />
        </button>
      </div>
      <nav className="mobile-menu-links" aria-label="Мобильная навигация">
        {[
          ["/", "Главная"],
          ["/Menu", "Меню"],
          ["/Account", "Мои заказы"],
          ["/Contacts", "Контакты"],
          ["/Delivery", "Доставка и оплата"],
          ["/About", "О кафе"],
          ["/Allergens", "Состав и аллергены"],
          ["/App#install-instructions", "Установить приложение"],
        ].map(([href, label]) => (
          <a key={href} href={href} onClick={close}>
            {label}
          </a>
        ))}
      </nav>
      <p className="mobile-menu-note">Любимое кафе — всегда под рукой.</p>
    </dialog>
  );
}
export function PublicApp({
  store,
  template,
}: {
  store: ShopStore;
  template: string;
}) {
  useShop(store);
  const [mobile, setMobile] = useState(false),
    [showConnection, setShowConnection] = useState(false),
    action = useAction(),
    catalog = store.app.catalog,
    settings = store.app.state?.settings ?? store.publicSettings,
    path = location.pathname.toLowerCase(),
    restored = useRef(false),
    install = useRef<{
      prompt: () => Promise<void>;
      userChoice: Promise<{ outcome: string }>;
    } | null>(null);
  useEffect(() => {
    if (store.phase === "connected") {
      setShowConnection(false);
      return;
    }
    const timer = setTimeout(() => setShowConnection(true), 2500);
    return () => clearTimeout(timer);
  }, [store.phase]);
  useEffect(() => {
    document.documentElement.classList.add("js-ready", "mh-welcome-ready");
    if ("serviceWorker" in navigator)
      void navigator.serviceWorker.register("/sw.js").catch(() => {});
    const prompt = (event: Event) => {
      event.preventDefault();
      install.current = event as unknown as typeof install.current;
    };
    window.addEventListener("beforeinstallprompt", prompt);
    return () => window.removeEventListener("beforeinstallprompt", prompt);
  }, []);
  useEffect(() => {
    if (!catalog || restored.current) return;
    restored.current = true;
    const saved = sessionStorage.getItem("mh.menu-position");
    if (
      path === "/menu" &&
      sessionStorage.getItem("mh.restore-menu") === "1" &&
      saved
    ) {
      try {
        const position = JSON.parse(saved) as { url: string; y: number };
        history.replaceState(null, "", position.url);
        scrollTo(0, position.y);
        sessionStorage.removeItem("mh.restore-menu");
        return;
      } catch {}
    }
    const target = location.hash
      ? document.getElementById(decodeURIComponent(location.hash.slice(1)))
      : null;
    if (target)
      requestAnimationFrame(() =>
        target.scrollIntoView({ block: "start", behavior: "instant" }),
      );
    if (new URLSearchParams(location.search).get("cart") === "1") {
      store.cartOpen = true;
      store.emit();
    }
  }, [catalog, path, store]);
  function click(event: MouseEvent) {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    const button = target.closest<HTMLElement>(
      "[data-cart-product],[data-favorite],[data-open-cart],[data-account],#mobile-menu-button,[data-platform-preview],#install-app,[data-product-link]",
    );
    if (!button) return;
    if (button.hasAttribute("data-product-link") && path === "/menu") {
      sessionStorage.setItem(
        "mh.menu-position",
        JSON.stringify({
          url: location.pathname + location.search + location.hash,
          y: scrollY,
        }),
      );
      return;
    }
    if (button.dataset.cartProduct)
      void store
        .adjust(
          button.dataset.cartProduct!,
          Number(button.dataset.productDelta ?? 1),
        )
        .catch((error) =>
          store.toast(
            error instanceof Error
              ? error.message
              : "Не удалось изменить корзину.",
          ),
        );
    else if (button.dataset.favorite)
      void action.run(() => store.favorite(button.dataset.favorite!));
    else if (button.hasAttribute("data-open-cart")) {
      store.cartOpen = true;
      store.emit();
    } else if (button.hasAttribute("data-account")) {
      void action.run(async () => {
        await store.ready;
        if (store.app.state?.authenticated) location.href = "/Account";
        else store.openAuth();
      });
    } else if (button.id === "mobile-menu-button") setMobile(!mobile);
    else if (button.dataset.platformPreview)
      store.toast(
        "Версия для " +
          button.dataset.platformPreview +
          " готовится. Меню и заказы уже доступны прямо на сайте.",
      );
    else if (button.id === "install-app")
      void action.run(async () => {
        if (install.current) {
          await install.current.prompt();
          const outcome = await install.current.userChoice;
          store.toast(
            outcome.outcome === "accepted"
              ? "Приложение добавлено."
              : "Можно установить приложение позже.",
          );
          install.current = null;
        } else {
          document
            .getElementById("install-instructions")
            ?.scrollIntoView({ behavior: reduced() ? "instant" : "smooth" });
          store.toast("Откройте инструкцию для своего устройства.");
        }
      });
  }
  let content;
  if (path === "/account") content = <Account store={store} />;
  else if (
    path === "/checkout" ||
    path === "/account" ||
    path === "/resetpassword" ||
    path.startsWith("/order/") ||
    path.startsWith("/payment/")
  )
    content =
      store.phase === "connected" ? (
        path === "/checkout" ? (
          <Checkout store={store} />
        ) : path === "/account" ? (
          <Account store={store} />
        ) : path === "/resetpassword" ? (
          <ResetPassword store={store} />
        ) : (
          <OrderPage store={store} payment={path.startsWith("/payment/")} />
        )
      ) : (
        <div className="container page-space" role="status">
          <h1>
            {store.phase === "failed"
              ? "Нет связи с кафе"
              : "Подключаемся к кафе…"}
          </h1>
          <p>
            Корзина сохранена.{" "}
            {store.phase === "failed"
              ? "Проверьте соединение и повторите."
              : "Скоро можно будет продолжить."}
          </p>
          <button className="btn" onClick={() => location.reload()}>
            Повторить
          </button>
        </div>
      );
  else if (catalog && path === "/")
    content = <Home store={store} catalog={catalog} html={template} />;
  else if (catalog && path === "/menu")
    content = <Menu store={store} catalog={catalog} />;
  else if (catalog && path.startsWith("/product/"))
    content = <ProductPage store={store} catalog={catalog} />;
  else if (catalog && path === "/allergens")
    content = (
      <div className="container page-space prose">
        <h1>Состав и аллергены</h1>
        <p>
          При аллергии уточните приготовление в кафе: на общем оборудовании
          возможен перекрёстный контакт.
        </p>
        {catalog.products.map((p) => (
          <article className="allergen-item" key={p.id}>
            <h2>
              <a href={"/Product/" + p.id}>{p.name}</a>
            </h2>
            <p>{p.ingredients}</p>
            <p>
              <strong>Базовая рецептура:</strong>{" "}
              {p.allergens.join(", ") || "Уточните состав в кафе."}
            </p>
            <Nutrition product={p} />
          </article>
        ))}
        <a className="btn" href="/Menu">
          Открыть меню
        </a>
      </div>
    );
  else content = <StaticContent html={template} settings={settings} />;
  return (
    <div className="app-root" onClick={click}>
      <a className="skip-link" href="#main">
        Перейти к содержимому
      </a>
      <header className="site-header">
        <div className="header-inner">
          <Header
            view={{
              cart: store.cartView(),
              favorites: store.app.state?.favorites ?? [],
            }}
            authenticated={store.app.state?.authenticated}
            connecting={store.phase !== "connected"}
            menuOpen={mobile}
          />
        </div>
        <MobileMenu open={mobile} close={() => setMobile(false)} />
      </header>
      <main id="main">{content}</main>
      <footer className="site-footer">
        <div className="container footer-grid">
          <Footer />
        </div>
        <div className="container footer-bottom">
          <span>© {new Date().getFullYear()} Mellow House</span>
        </div>
      </footer>
      <CartDialog store={store} />
      <Auth store={store} />
      {(store.message || action.error) && (
        <div className="toast" role="status" aria-live="polite">
          <span>{action.error || store.message}</span>
        </div>
      )}
      {store.phase !== "connected" &&
        (showConnection || store.phase === "failed") && (
          <div className="server-connection" role="status">
            {store.phase === "failed"
              ? "Пока нет связи с кафе. Выбранные блюда сохранены в браузере."
              : "Выбирайте блюда — корзина работает. Подключаем кафе, чтобы сверить цены и оформить заказ."}
            {store.phase === "failed" && (
              <button onClick={() => location.reload()}>Повторить</button>
            )}
          </div>
        )}
    </div>
  );
}
