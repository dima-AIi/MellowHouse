import { AppBoundary } from "./ui";
import "vite/modulepreload-polyfill";
import { createRoot } from "react-dom/client";
import { ShopStore } from "./store";
import { PublicApp } from "./public-app";

const template =
  document.querySelector("#main")?.innerHTML ??
  '<div class="container page-space"><h1>Страница не найдена</h1><a href="/Menu">Открыть меню</a></div>';
const host = document.createElement("div");
host.id = "react-app";
document.body.replaceChildren(host);
const store = new ShopStore();
document.documentElement.dataset.uiRuntime = "react-typescript-full";
createRoot(host).render(
  <AppBoundary>
    <PublicApp store={store} template={template} />
  </AppBoundary>,
);
