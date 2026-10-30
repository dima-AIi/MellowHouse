import type { Catalog, PublicSettings } from "./types";
export const publicCacheKey = "mh.public-menu.v27";
export interface PublicCache {
  catalog: Catalog;
  settings?: PublicSettings;
  savedAt: number;
}
export function readPublicCache(storage: Storage): PublicCache | null {
  try {
    const value = JSON.parse(
      storage.getItem(publicCacheKey) ?? "null",
    ) as PublicCache | null;
    if (
      !value ||
      !Number.isFinite(value.savedAt) ||
      Date.now() - value.savedAt > 86400000 ||
      !Array.isArray(value.catalog?.products) ||
      !Array.isArray(value.catalog?.categories) ||
      value.catalog.products.length > 500 ||
      value.catalog.products.some(
        (p) =>
          typeof p.id !== "string" ||
          !Array.isArray(p.groups) ||
          !Array.isArray(p.allergens),
      )
    )
      return null;
    return value;
  } catch {
    return null;
  }
}
export function writePublicCache(storage: Storage, value: PublicCache) {
  // This cache contains the public menu only: never cookies, CSRF, profile, orders or a server cart.
  try {
    storage.setItem(
      publicCacheKey,
      JSON.stringify({
        catalog: value.catalog,
        settings: value.settings,
        savedAt: value.savedAt,
      }),
    );
  } catch {}
}
