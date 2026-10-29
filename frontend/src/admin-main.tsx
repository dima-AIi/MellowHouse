import { AppBoundary } from "./ui";
import "vite/modulepreload-polyfill";
import { createRoot } from "react-dom/client";
import { ShopStore } from "./store";
import { AdminApp } from "./admin-app";
const host = document.createElement("div");
host.id = "react-admin";
document.body.replaceChildren(host);
document.documentElement.dataset.uiRuntime = "react-typescript-full";
createRoot(host).render(
  <AppBoundary>
    <AdminApp store={new ShopStore()} />
  </AppBoundary>,
);
