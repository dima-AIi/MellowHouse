import {
  Component,
  useEffect,
  useId,
  useRef,
  useState,
  type ReactNode,
  type FormEvent,
  type ChangeEvent,
  type InputHTMLAttributes,
} from "react";
import { ApiError } from "./store";
export const errorText = (error: unknown) =>
  error instanceof Error ? error.message : "Не удалось выполнить действие.";
export function Modal({
  open,
  title,
  children,
  onClose,
  wide = false,
  className = "",
}: {
  open: boolean;
  title: string;
  children: ReactNode;
  onClose: () => void;
  wide?: boolean;
  className?: string;
}) {
  const ref = useRef<HTMLDialogElement>(null),
    id = useId();
  useEffect(() => {
    const dialog = ref.current;
    if (open && !dialog?.open) dialog?.showModal();
    if (!open && dialog?.open) dialog.close();
  }, [open]);
  return (
    <dialog
      ref={ref}
      className={className || `admin-dialog ${wide ? "admin-dialog-wide" : ""}`}
      aria-labelledby={id}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
    >
      <div className="dialog-heading">
        <h2 id={id}>{title}</h2>
        <button
          type="button"
          className="icon-btn"
          onClick={onClose}
          aria-label="Закрыть"
        >
          ×
        </button>
      </div>
      {children}
    </dialog>
  );
}
export function Field({
  label,
  error,
  ...props
}: InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string }) {
  const id = useId();
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        {...props}
        onInput={
          props.onInput ??
          (["date", "datetime-local", "time"].includes(props.type ?? "")
            ? (event) =>
                props.onChange?.(
                  event as unknown as ChangeEvent<HTMLInputElement>,
                )
            : undefined)
        }
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? id + "-error" : undefined}
      />
      {error && (
        <p id={id + "-error"} className="form-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
export function ListField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string[];
  onChange: (value: string[]) => void;
}) {
  const [text, setText] = useState(value.join(", "));
  return (
    <Field
      label={label}
      value={text}
      onChange={(event) => {
        setText(event.target.value);
        onChange(
          event.target.value
            .split(",")
            .map((value) => value.trim())
            .filter(Boolean),
        );
      }}
    />
  );
}
export function Select({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: [string, string][];
}) {
  const id = useId();
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
        {options.map(([value, label]) => (
          <option key={value} value={value}>
            {label}
          </option>
        ))}
      </select>
    </div>
  );
}
export function useAction() {
  const [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [field, setField] = useState<string | undefined>(),
    lock = useRef(false);
  async function run(action: () => Promise<unknown>) {
    if (lock.current) return false;
    lock.current = true;
    setBusy(true);
    setError("");
    setField(undefined);
    try {
      await action();
      return true;
    } catch (error) {
      setError(errorText(error));
      if (error instanceof ApiError) setField(error.field);
      return false;
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return { busy, error, field, run, setError };
}
export function Form({
  children,
  onSubmit,
  busy,
  error,
}: {
  children: ReactNode;
  onSubmit: () => void;
  busy: boolean;
  error?: string;
}) {
  function submit(e: FormEvent) {
    e.preventDefault();
    onSubmit();
  }
  return (
    <form onSubmit={submit}>
      <fieldset
        disabled={busy}
        style={{ border: 0, padding: 0, margin: 0, minWidth: 0 }}
      >
        {children}
      </fieldset>
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}
export function Empty({
  title,
  text,
  children,
}: {
  title: string;
  text?: string;
  children?: ReactNode;
}) {
  return (
    <div className="empty-state">
      <h2>{title}</h2>
      {text && <p>{text}</p>}
      {children}
    </div>
  );
}
export function useResource<T>(load: () => Promise<T>, key: string) {
  const [value, setValue] = useState<T>(),
    [error, setError] = useState(""),
    version = useRef(0);
  async function reload() {
    const current = ++version.current;
    try {
      const next = await load();
      if (current === version.current) {
        setValue(next);
        setError("");
      }
    } catch (error) {
      if (current === version.current) setError(errorText(error));
    }
  }
  useEffect(() => {
    void reload();
    return () => {
      version.current++;
    };
  }, [key]);
  return { value, setValue, error, reload };
}

export class AppBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <main className="container page-space" role="alert">
        <h1>Не удалось показать страницу</h1>
        <p>Обновите страницу. Сохранённая корзина останется в браузере.</p>
        <button className="btn" onClick={() => location.reload()}>
          Обновить страницу
        </button>
        <a className="btn btn-outline" href="/">
          На главную
        </a>
      </main>
    ) : (
      this.props.children
    );
  }
}

export function useUnsavedChanges(dirty: boolean) {
  useEffect(() => {
    if (!dirty) return;
    const guard = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", guard);
    return () => window.removeEventListener("beforeunload", guard);
  }, [dirty]);
}
