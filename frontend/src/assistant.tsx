import { useEffect, useRef, useState, type FormEvent } from "react";
import { readChat, writeChat } from "./sessions";
import { image } from "./media";
import { Configurator, money } from "./components";
import type {
  AssistantAnswer,
  AssistantRequest,
  Catalog,
  ChatSession,
  ShopRuntime,
  Product,
  Selections,
} from "./types";

const greeting =
  "Здравствуйте! Какую паузу устроим сегодня — с любимым кофе или горячим сэндвичем?";
const initialChoices = [
  "Подобрать кофе",
  "Что перекусить?",
  "Покажи мой последний заказ",
];
const owner = (runtime: ShopRuntime) =>
  runtime.app.state?.authenticated
    ? "u:" + runtime.app.state.profile!.id
    : "guest";
function fresh(runtime: ShopRuntime): ChatSession {
  return {
    owner: owner(runtime),
    entries: [{ role: "assistant", text: greeting }],
    draft: "",
    recentQuestions: [],
    lastProductIds: [],
    suggestions: initialChoices,
    scroll: 0,
    pending: null,
  };
}
const errorText = (error: unknown) =>
  error instanceof Error
    ? error.message
    : "Не удалось получить ответ. Повторите сообщение.";

export function Assistant({
  runtime,
  catalog,
}: {
  runtime: ShopRuntime;
  catalog: Catalog;
}) {
  const [model, setModel] = useState(() => fresh(runtime));
  const modelRef = useRef(model);
  modelRef.current = model;
  const [initialized, setInitialized] = useState(false),
    [busy, setBusy] = useState(false),
    busyRef = useRef(false);
  const generation = useRef(0);
  const log = useRef<HTMLDivElement>(null),
    reply = useRef<HTMLDivElement>(null),
    dialog = useRef<HTMLDialogElement>(null);
  const [config, setConfig] = useState<{
      key: string;
      product: Product;
      selections: Selections;
    } | null>(null),
    [adding, setAdding] = useState(false);
  useEffect(() => {
    let active = true;
    runtime.ready
      .then(() => {
        if (!active) return;
        const saved = readChat(sessionStorage);
        const restored =
          saved?.owner === owner(runtime) ? saved : fresh(runtime);
        setModel({
          ...restored,
          draft: modelRef.current.draft || restored.draft,
        });
        setInitialized(true);
        requestAnimationFrame(() => {
          if (log.current) log.current.scrollTop = restored.scroll ?? 0;
        });
      })
      .catch(() => {
        if (active)
          setModel((previous) => ({
            ...previous,
            entries: [
              ...previous.entries,
              {
                role: "assistant",
                text: "Пока нет связи с кафе. Сообщение можно написать сейчас и отправить после подключения.",
              },
            ],
          }));
      });
    const reset = () => {
      generation.current++;
      setModel(fresh(runtime));
    };
    const save = () => {
      if (initializedRef.current)
        writeChat(sessionStorage, {
          ...modelRef.current,
          owner: owner(runtime),
          scroll: log.current?.scrollTop ?? 0,
        });
    };
    document.addEventListener("mh:chat-reset", reset);
    window.addEventListener("pagehide", save);
    return () => {
      active = false;
      document.removeEventListener("mh:chat-reset", reset);
      window.removeEventListener("pagehide", save);
    };
  }, [runtime]);
  const initializedRef = useRef(initialized);
  initializedRef.current = initialized;
  useEffect(() => {
    if (initialized)
      writeChat(sessionStorage, {
        ...model,
        owner: owner(runtime),
        scroll: log.current?.scrollTop ?? 0,
      });
  }, [model, initialized, runtime]);
  useEffect(() => {
    if (config && !dialog.current?.open) dialog.current?.showModal();
  }, [config]);

  async function ask(
    message: string,
    ids?: string[],
    retry?: AssistantRequest,
  ) {
    if (busyRef.current || !message.trim()) return;
    busyRef.current = true;
    setBusy(true);
    const startedGeneration = generation.current;
    try {
      await runtime.ready;
      if (startedGeneration !== generation.current) return;
      const startedOwner = owner(runtime);
      const current = modelRef.current;
      const body = retry ?? {
        message,
        recentQuestions: current.recentQuestions.slice(-4),
        lastProductIds: ids ?? current.lastProductIds,
        requestId: crypto.randomUUID(),
      };
      setModel((previous) => ({
        ...previous,
        draft: "",
        pending: body,
        entries: retry
          ? previous.entries
          : [...previous.entries, { role: "customer", text: message }],
      }));
      try {
        const answer = await runtime.request<AssistantAnswer>(
          "/api/assistant",
          body,
          "POST",
        );
        if (
          startedGeneration !== generation.current ||
          startedOwner !== owner(runtime)
        )
          return;
        if (answer.cart) runtime.setCart(answer.cart);
        const productIds = (answer.productIds ?? []).slice(0, 6);
        setModel((previous) => ({
          ...previous,
          pending: null,
          recentQuestions: [...previous.recentQuestions, message].slice(-4),
          lastProductIds: productIds,
          suggestions: answer.suggestions ?? [],
          entries: [
            ...previous.entries,
            {
              role: "assistant",
              text: answer.text,
              question: message,
              answer: {
                productIds,
                addedProductIds: answer.addedProductIds,
                requiresLogin: answer.requiresLogin,
                ...(answer.cart ? { cartTotal: answer.cart.subtotal } : {}),
              },
            },
          ],
        }));
      } catch (error) {
        if (
          startedGeneration !== generation.current ||
          startedOwner !== owner(runtime)
        )
          return;
        const status = (error as { status?: number })?.status;
        setModel((previous) => ({
          ...previous,
          pending: !status || status >= 500 ? body : null,
          entries: [
            ...previous.entries,
            { role: "assistant", text: errorText(error) },
          ],
        }));
      }
    } catch (error) {
      runtime.toast(errorText(error));
    } finally {
      busyRef.current = false;
      setBusy(false);
      requestAnimationFrame(() => {
        const host = log.current,
          target = reply.current;
        if (host && target)
          host.scrollTop +=
            target.getBoundingClientRect().top -
            host.getBoundingClientRect().top -
            8;
        host
          ?.closest(".assistant-card")
          ?.scrollIntoView({ block: "nearest", behavior: "instant" });
      });
    }
  }
  function configure(product: Product) {
    setConfig({
      key: crypto.randomUUID(),
      product,
      selections: runtime.defaults(product),
    });
  }
  function closeConfig() {
    dialog.current?.close();
    setConfig(null);
  }
  async function addConfigured() {
    if (!config || adding) return;
    setAdding(true);
    try {
      await runtime.ready;
      runtime.setCart(
        await runtime.request(
          "/api/cart",
          {
            productId: config.product.id,
            selections: config.selections,
            quantity: 1,
          },
          "POST",
        ),
      );
      const name = config.product.name;
      closeConfig();
      setModel((previous) => ({
        ...previous,
        entries: [
          ...previous.entries,
          {
            role: "assistant",
            text:
              name +
              " — в корзине. Можно продолжить выбор или перейти к оформлению.",
          },
        ],
      }));
      runtime.toast("Добавлено в корзину");
    } catch (error) {
      runtime.toast(errorText(error));
    } finally {
      setAdding(false);
    }
  }
  function login(question: string) {
    runtime.openAuth?.(() => {
      void ask(question);
    });
  }
  function submit(event: FormEvent) {
    event.preventDefault();
    void ask(model.draft.trim());
  }
  return (
    <>
      <header>
        <span className="assistant-avatar">✧</span>
        <div>
          <h3>Помощник Mellow House</h3>
          <p>Выбор блюд и ваши прошлые заказы</p>
        </div>
      </header>
      <div
        id="chat-messages"
        className="chat-messages"
        role="log"
        aria-live="polite"
        ref={log}
        onScroll={() => {
          if (initialized)
            writeChat(sessionStorage, {
              ...modelRef.current,
              owner: owner(runtime),
              scroll: log.current?.scrollTop ?? 0,
            });
        }}
      >
        {model.entries.map((entry, index) => (
          <div className="chat-entry" key={index}>
            <div
              className={`chat-message ${entry.role}`}
              ref={index === model.entries.length - 1 ? reply : undefined}
            >
              {entry.text}
            </div>
            {entry.answer?.requiresLogin && (
              <button
                type="button"
                className="btn btn-outline chat-history-login"
                onClick={() =>
                  void login(entry.question ?? "Покажи мой последний заказ")
                }
              >
                Войти и посмотреть историю
              </button>
            )}
            {(entry.answer?.productIds ?? []).map((id) => {
              const product = catalog.products.find((p) => p.id === id);
              if (!product?.available) return null;
              const added = entry.answer?.addedProductIds?.includes(id);
              return (
                <div className="chat-product" key={id}>
                  <img
                    src={image(product.image)}
                    alt=""
                    width="64"
                    height="64"
                  />
                  <div>
                    <strong>{product.name}</strong>
                    <span>
                      {money(product.price)} · {product.portion}
                    </span>
                    <small>{product.description}</small>
                  </div>
                  <div className="chat-product-actions">
                    {added ? (
                      <span className="chat-added">Было добавлено ✓</span>
                    ) : (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() =>
                          void ask("Добавь этот товар в корзину", [id])
                        }
                      >
                        Добавить
                      </button>
                    )}
                    <button type="button" onClick={() => configure(product)}>
                      Настроить
                    </button>
                  </div>
                </div>
              );
            })}
            {entry.answer?.cartTotal !== undefined && (
              <button
                type="button"
                className="btn btn-outline chat-open-cart"
                data-open-cart=""
              >
                Проверить корзину
              </button>
            )}
          </div>
        ))}
        {busy && (
          <div
            className="chat-message assistant chat-typing"
            aria-label="Помощник отвечает"
          >
            <span />
            <span />
            <span />
          </div>
        )}
        {model.pending && !busy && (
          <button
            type="button"
            className="text-button"
            onClick={() =>
              void ask(model.pending!.message, undefined, model.pending!)
            }
          >
            Повторить сообщение
          </button>
        )}
      </div>
      <div className="chat-suggestions">
        {model.suggestions.map((value) => (
          <button
            type="button"
            key={value}
            disabled={busy}
            onClick={() => void ask(value)}
          >
            {value === "Покажи мой последний заказ"
              ? "Мой последний заказ"
              : value}
          </button>
        ))}
      </div>
      <form id="chat-form" method="post" onSubmit={submit}>
        <label className="sr-only" htmlFor="chat-input">
          Ваш вопрос
        </label>
        <input
          id="chat-input"
          placeholder="Чего хочется сегодня?"
          maxLength={500}
          required
          autoComplete="off"
          value={model.draft}
          onChange={(e) =>
            setModel((previous) => ({ ...previous, draft: e.target.value }))
          }
        />
        <button type="submit" aria-label="Отправить вопрос" disabled={busy}>
          ↑
        </button>
      </form>
      <dialog
        id="react-assistant-product-dialog"
        className="edit-dialog"
        ref={dialog}
        aria-labelledby="react-assistant-product-title"
        onCancel={closeConfig}
      >
        <div className="dialog-heading">
          <h2 id="react-assistant-product-title">Настроим ваш заказ</h2>
          <button
            type="button"
            className="icon-btn"
            aria-label="Закрыть"
            onClick={closeConfig}
          >
            ×
          </button>
        </div>
        {config && (
          <>
            <div className="edit-preview">
              <img
                src={image(config.product.image)}
                alt={config.product.name}
                width="90"
                height="90"
              />
              <div>
                <h3>{config.product.name}</h3>
                <p>{config.product.portion}</p>
              </div>
            </div>
            <div className="configurator">
              <Configurator
                key={config.key}
                product={config.product}
                initial={config.selections}
                onInvalid={runtime.toast}
                onChange={(selection) =>
                  setConfig((previous) =>
                    previous ? { ...previous, selections: selection } : null,
                  )
                }
              />
            </div>
            <strong className="large-price">
              {money(
                runtime.calculated(config.product, config.selections).price,
              )}
            </strong>
            <button
              type="button"
              className="btn wide"
              disabled={adding}
              onClick={() => void addConfigured()}
            >
              Добавить в корзину ·{" "}
              {money(
                runtime.calculated(config.product, config.selections).price,
              )}
            </button>
          </>
        )}
        <p className="small muted">
          Добавление не оформляет заказ. Его можно проверить и изменить в
          корзине.
        </p>
      </dialog>
    </>
  );
}
