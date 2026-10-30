import { useEffect, useState } from "react";
import type { CartLine } from "./types";
import { ShopStore, useShop } from "./store";
import { Configurator, Icon, money } from "./components";
import { image } from "./media";
import { Field, Form, Modal, Empty, useAction } from "./ui";
import { validPhone } from "./sessions";

export function Auth({
  store,
  admin = false,
}: {
  store: ShopStore;
  admin?: boolean;
}) {
  useShop(store);
  const [mode, setMode] = useState<
      "phone" | "code" | "email" | "register" | "forgot"
    >(admin ? "email" : "phone"),
    [email, setEmail] = useState(""),
    [password, setPassword] = useState(""),
    [name, setName] = useState(""),
    [phone, setPhone] = useState(""),
    [phoneError, setPhoneError] = useState(""),
    [code, setCode] = useState(""),
    [challenge, setChallenge] = useState(""),
    [demo, setDemo] = useState(""),
    [status, setStatus] = useState(""),
    [visible, setVisible] = useState(false);
  const action = useAction();
  useEffect(() => {
    if (!store.authOpen) setPassword("");
  }, [store.authOpen]);
  const close = () => {
    store.authOpen = false;
    store.afterAuth = undefined;
    store.emit();
  };
  async function submit() {
    if (mode === "phone" && !validPhone(phone)) {
      setPhoneError("Введите полный номер: +7 или 8 и десять цифр. Например, +7 (999) 123-45-67.");
      document.querySelector<HTMLInputElement>('.auth-dialog input[type="tel"]')?.focus();
      return;
    }
    await action.run(async () => {
      await store.ready;
      if (mode === "phone") {
        if (!validPhone(phone))
          throw Error("Введите телефон полностью: +7 и десять цифр.");
        const reply = await store.request<{
          challengeId: string;
          demoCode?: string;
        }>("/api/auth/challenge", { contact: phone, channel: "phone" }, "POST");
        setChallenge(reply.challengeId);
        setDemo(reply.demoCode ?? "");
        setMode("code");
        return;
      }
      if (mode === "forgot") {
        const reply = await store.request<{ message: string }>(
          "/api/auth/forgot",
          { email },
          "POST",
        );
        setStatus(reply.message);
        return;
      }
      if (
        mode === "register" &&
        (!name.trim() ||
          password.length < 10 ||
          !/[\p{L}]/u.test(password) ||
          !/[\p{Nd}]/u.test(password))
      )
        throw Error("Укажите имя и пароль от 10 символов с буквой и цифрой.");
      if (mode === "code")
        await store.request(
          "/api/auth/verify",
          { challengeId: challenge, code },
          "POST",
        );
      else
        await store.request(
          "/api/auth/" +
            (admin ? "admin" : mode === "register" ? "register" : "login"),
          { email, password, name, ...(admin ? { login: email } : {}) },
          "POST",
        );
      await store.signedIn();
      if (admin) location.href = "/Admin";
    });
  }
  const content = (
    <>
      <h2>
        {admin
          ? "Вход для команды"
          : mode === "forgot"
            ? "Восстановить доступ"
            : mode === "code"
              ? "Введите код"
              : "Добро пожаловать в Mellow House"}
      </h2>
      {!admin && (
        <>
          <p className="saved-note">✓ Корзина сохранена</p>
          <div className="auth-tabs">
            <button
              type="button"
              className={mode === "phone" || mode === "code" ? "active" : ""}
              onClick={() => {
                setMode("phone");
                action.setError("");
              }}
            >
              Телефон
            </button>
            <button
              type="button"
              className={mode !== "phone" && mode !== "code" ? "active" : ""}
              onClick={() => {
                setMode("email");
                action.setError("");
              }}
            >
              Почта и пароль
            </button>
          </div>
        </>
      )}
      <Form
        busy={action.busy}
        error={action.error}
        onSubmit={() => void submit()}
      >
        {mode === "phone" ? (
          <Field
            label="Телефон"
            type="tel"
            name="phone"
            inputMode="tel"
            maxLength={22}
            error={phoneError}
            autoComplete="tel"
            value={phone}
            onChange={(e) => {
              setPhone(e.target.value);
              setPhoneError("");
            }}
            onBlur={() => {
              if (phone && !validPhone(phone)) setPhoneError("Введите полный номер: +7 или 8 и десять цифр.");
            }}
            placeholder="+7 (999) 123-45-67"
            required
          />
        ) : mode === "code" ? (
          <>
            <Field
              label="Код подтверждения"
              inputMode="numeric"
              autoComplete="one-time-code"
              pattern="[0-9]{6}"
              maxLength={6}
              value={code}
              onChange={(e) => setCode(e.target.value)}
              required
            />
            {demo && (
              <p className="small muted">Код: {demo} · действует 5 минут</p>
            )}
          </>
        ) : (
          <>
            {mode === "register" && (
              <Field
                label="Имя"
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoComplete="given-name"
                maxLength={80}
                required
              />
            )}
            <Field
              label={admin ? "Почта или логин" : "Почта"}
              type={admin ? "text" : "email"}
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              maxLength={140}
              required
            />
            {mode !== "forgot" && (
              <>
                <Field
                  label="Пароль"
                  type={visible ? "text" : "password"}
                  autoComplete={
                    mode === "register" ? "new-password" : "current-password"
                  }
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  maxLength={128}
                  required
                />
                <button
                  type="button"
                  className="text-button"
                  onClick={() => setVisible(!visible)}
                  aria-label={visible ? "Скрыть пароль" : "Показать пароль"}
                >
                  {visible ? "Скрыть пароль" : "Показать пароль"}
                </button>
              </>
            )}
          </>
        )}
        <button className="btn wide" type="submit">
          {mode === "phone"
            ? "Получить код"
            : mode === "code"
              ? "Подтвердить"
              : mode === "register"
                ? "Создать аккаунт"
                : mode === "forgot"
                  ? "Запросить восстановление"
                  : "Войти"}
        </button>
      </Form>
      {status && <p role="status">{status}</p>}
      {!admin && (
        <div className="auth-secondary">
          {mode === "code" ? (
            <button
              type="button"
              className="text-button"
              onClick={() => setMode("phone")}
            >
              Изменить телефон
            </button>
          ) : mode === "email" || mode === "register" ? (
            <>
              <button
                type="button"
                className="text-button"
                onClick={() =>
                  setMode(mode === "register" ? "email" : "register")
                }
              >
                {mode === "register"
                  ? "Уже есть аккаунт? Войти"
                  : "Создать аккаунт"}
              </button>
              <button
                type="button"
                className="text-button"
                onClick={() => setMode("forgot")}
              >
                Не помню пароль
              </button>
            </>
          ) : mode === "forgot" ? (
            <button
              type="button"
              className="text-button"
              onClick={() => setMode("email")}
            >
              Вернуться ко входу
            </button>
          ) : null}
        </div>
      )}
    </>
  );
  return admin ? (
    <div className="auth-dialog admin-login-panel">
      {content}
      <a href="/">Вернуться на сайт</a>
    </div>
  ) : (
    <Modal
      open={store.authOpen}
      title="Вход в Mellow House"
      onClose={close}
      className="auth-dialog"
    >
      {content}
    </Modal>
  );
}
export function Totals({
  subtotal,
  delivery,
}: {
  subtotal: number;
  delivery: number;
}) {
  return (
    <div className="cart-totals">
      <div className="total-row">
        <span>Товары</span>
        <strong>{money(subtotal)}</strong>
      </div>
      <div className="total-row">
        <span>{delivery ? "Доставка" : "Самовывоз"}</span>
        <strong>{delivery ? money(delivery) : "Бесплатно"}</strong>
      </div>
      <div className="total-row grand">
        <span>Итого</span>
        <strong>{money(subtotal + delivery)}</strong>
      </div>
    </div>
  );
}
export function Method({ store }: { store: ShopStore }) {
  useShop(store);
  return (
    <div className="segmented">
      {(["pickup", "delivery"] as const).map((method) => (
        <button
          type="button"
          key={method}
          className={store.app.method === method ? "active" : ""}
          aria-pressed={store.app.method === method}
          onClick={() =>
            void store
              .setMethod(method)
              .catch((error) => store.toast(error.message))
          }
        >
          <Icon name={method === "pickup" ? "pin" : "truck"} />
          {method === "pickup" ? "С собой" : "Доставка"}
        </button>
      ))}
    </div>
  );
}
export function CartDialog({ store }: { store: ShopStore }) {
  useShop(store);
  const cart = store.cartView(),
    unavailable = cart.items.some(
      (line) => line.available === false || !line.product.available,
    ),
    action = useAction(),
    [clear, setClear] = useState(false),
    [editing, setEditing] = useState<CartLine | null>(null),
    [removed, setRemoved] = useState<CartLine | null>(null);
  const close = () => {
    store.cartOpen = false;
    store.emit();
  };
  async function remove(line: CartLine) {
    if (await action.run(() => store.remove(line.id))) setRemoved(line);
  }
  return (
    <>
      <Modal
        open={store.cartOpen}
        title="Корзина"
        onClose={close}
        className="cart-drawer"
      >
        {cart.count > 0 && (
          <button
            type="button"
            className="icon-btn cart-clear"
            aria-label="Очистить корзину"
            onClick={() => setClear(true)}
          >
            <Icon name="trash" />
          </button>
        )}
        {(store.phase !== "connected" || store.notice) && (
          <div className="notice" role="status">
            {store.notice ||
              "Выбирайте блюда — корзина сохраняется в браузере. Оформление станет доступно после подключения."}
          </div>
        )}
        <div id="cart-lines" className="cart-lines">
          {cart.items.length ? (
            cart.items.map((line) => (
              <article className="cart-line" key={line.id}>
                <img
                  src={image(line.product.image)}
                  alt={line.product.name}
                  width="90"
                  height="90"
                />
                <div className="cart-line-copy">
                  <h3>{line.product.name}</h3>
                  {(line.available === false || !line.product.available) && (
                    <p className="form-error">
                      Сейчас недоступен — удалите позицию перед оформлением.
                    </p>
                  )}
                  <p>{line.summary.join(", ") || line.product.portion}</p>
                  <div className="quantity">
                    <button
                      disabled={action.busy}
                      type="button"
                      aria-label={"Уменьшить количество " + line.product.name}
                      onClick={() =>
                        void action.run(() =>
                          line.quantity === 1
                            ? store.remove(line.id)
                            : store.edit(
                                line.id,
                                line.selections,
                                line.quantity - 1,
                              ),
                        )
                      }
                    >
                      −
                    </button>
                    <output>{line.quantity}</output>
                    <button
                      disabled={action.busy}
                      type="button"
                      aria-label={"Увеличить количество " + line.product.name}
                      onClick={() =>
                        void action.run(() =>
                          store.edit(
                            line.id,
                            line.selections,
                            line.quantity + 1,
                          ),
                        )
                      }
                    >
                      +
                    </button>
                  </div>
                  <button
                    type="button"
                    className="text-button"
                    onClick={() => setEditing(structuredClone(line))}
                  >
                    Изменить
                  </button>
                </div>
                <div>
                  <button
                    type="button"
                    className="icon-btn"
                    aria-label={"Удалить " + line.product.name}
                    disabled={action.busy}
                    onClick={() => void remove(line)}
                  >
                    <Icon name="trash" />
                  </button>
                  <strong>{money(line.total)}</strong>
                </div>
              </article>
            ))
          ) : (
            <Empty
              title="В корзине пока пусто"
              text="Добавьте любимые блюда из нашего меню"
            >
              <a className="btn wide" href="/Menu">
                Открыть меню
              </a>
            </Empty>
          )}
        </div>
        {removed && (
          <p role="status">
            {removed.product.name} удалён.{" "}
            <button
              type="button"
              className="text-button"
              onClick={() =>
                void action.run(async () => {
                  await store.add(
                    removed.product,
                    removed.selections,
                    removed.quantity,
                  );
                  setRemoved(null);
                })
              }
            >
              Отменить удаление
            </button>
          </p>
        )}
        {action.error && (
          <p className="form-error" role="alert">
            {action.error}
          </p>
        )}
        {!!cart.items.length && (
          <div id="cart-bottom">
            <h3>Способ получения</h3>
            <Method store={store} />
            <Totals
              subtotal={cart.subtotal}
              delivery={
                store.app.method === "delivery"
                  ? (store.app.state?.deliveryPrice ?? 190)
                  : 0
              }
            />
            <button
              type="button"
              className="btn wide"
              disabled={store.phase !== "connected" || unavailable}
              onClick={() => {
                close();
                location.href = "/Checkout";
              }}
            >
              {store.phase === "connected"
                ? "Оформить заказ"
                : "Оформить после подключения"}
            </button>
          </div>
        )}
      </Modal>
      <Modal
        open={clear}
        title="Очистить корзину?"
        onClose={() => setClear(false)}
      >
        <p>Все позиции будут удалены. Вы сможете добавить их заново из меню.</p>
        <div className="dialog-actions">
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => setClear(false)}
          >
            Отмена
          </button>
          <button
            type="button"
            className="btn"
            disabled={action.busy}
            onClick={() =>
              void action.run(async () => {
                await store.remove();
                setClear(false);
              })
            }
          >
            Очистить
          </button>
        </div>
      </Modal>
      <Modal
        open={!!editing}
        title="Изменить товар"
        onClose={() => setEditing(null)}
        className="edit-dialog"
      >
        {editing && (
          <>
            <div className="edit-preview">
              <img
                src={image(editing.product.image)}
                alt={editing.product.name}
                width="90"
                height="90"
              />
              <h3>{editing.product.name}</h3>
            </div>
            <div className="configurator">
              <Configurator
                key={editing.id}
                product={editing.product}
                initial={editing.selections}
                onInvalid={store.toast}
                onChange={(selections) =>
                  setEditing((previous) =>
                    previous ? { ...previous, selections } : null,
                  )
                }
              />
            </div>
            <div className="buy-row">
              <div className="quantity">
                <button
                  type="button"
                  aria-label="Уменьшить количество"
                  onClick={() =>
                    setEditing({
                      ...editing,
                      quantity: Math.max(1, editing.quantity - 1),
                    })
                  }
                >
                  −
                </button>
                <output>{editing.quantity}</output>
                <button
                  type="button"
                  aria-label="Увеличить количество"
                  onClick={() =>
                    setEditing({
                      ...editing,
                      quantity: Math.min(99, editing.quantity + 1),
                    })
                  }
                >
                  +
                </button>
              </div>
              <strong className="large-price">
                {money(
                  store.calculated(editing.product, editing.selections).price *
                    editing.quantity,
                )}
              </strong>
            </div>
            <p className="form-error" role="alert">
              {action.error}
            </p>
            <button
              className="btn wide"
              disabled={action.busy}
              onClick={() =>
                void action.run(async () => {
                  await store.edit(
                    editing.id,
                    editing.selections,
                    editing.quantity,
                  );
                  setEditing(null);
                })
              }
            >
              Сохранить изменения
            </button>
          </>
        )}
      </Modal>
    </>
  );
}
