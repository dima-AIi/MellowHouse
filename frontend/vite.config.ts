import { defineConfig } from "vitest/config";

export default defineConfig({
  base: "/react/",
  build: {
    outDir: "../src/MellowHouse.Web/wwwroot/react",
    emptyOutDir: true,
    target: "es2022",
    manifest: true,
    rollupOptions: {
      input: {
        storefront: "src/main.tsx",
        admin: "src/admin-main.tsx",
        welcome: "src/welcome.ts",
      },
      output: {
        entryFileNames: "[name].js",
        chunkFileNames: "chunks/[name]-[hash].js",
      },
    },
  },
  test: {
    environment: "jsdom",
    setupFiles: ["tests/setup.ts"],
    restoreMocks: true,
  },
});
