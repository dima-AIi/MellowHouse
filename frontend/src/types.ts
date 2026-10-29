export interface ProductOption {id: string; label: string; price: number; addAllergens?: string[]; removeAllergens?: string[]}
export interface OptionGroup {id: string; label: string; options: ProductOption[]; defaults: string[]; multiple: boolean; required: boolean; min?: number | null; max?: number | null}
export type Selections = Record<string, string[]>;
export interface NutritionFacts {calories: number; protein: number; fat: number; carbs: number; estimated: boolean}
export interface Product {id: string; categoryId: string; name: string; price: number; portion: string; description: string; ingredients: string; image: string; available: boolean; popular: boolean; recommended: boolean; groups: OptionGroup[]; allergens: string[]; nutrition?: string; nutritionFacts?: NutritionFacts | null}
export interface Category {id: string; name: string; image: string; onHome: boolean; sortOrder: number}
export interface Catalog {products: Product[]; categories: Category[]}
export interface CartLine {id: string; product: Product; quantity: number; selections: Selections; unitPrice: number; total: number; summary: string[]}
export interface Cart {items: CartLine[]; count: number; subtotal: number; revision?: number}
export interface PublicSettings {marketingTitle: string; menuTitle: string; menuText: string; faq: {question: string; answer: string}[]}
export interface SessionState {csrfToken: string; authenticated: boolean; profile: {id: string; role: string; name: string} | null; favorites: string[]; cart: Cart; settings: PublicSettings}
export interface RuntimeState {state: SessionState | null; catalog: Catalog | null; method: 'pickup' | 'delivery'}
export interface AssistantRequest {message: string; recentQuestions: string[]; lastProductIds: string[]; requestId: string}
export interface AssistantAnswer {text: string; productIds: string[]; suggestions: string[]; cart?: Cart; addedProductIds?: string[]; requiresLogin?: boolean}
export interface ChatAnswer {productIds: string[]; suggestions?: string[]; addedProductIds?: string[]; requiresLogin?: boolean; cartTotal?: number}
export interface ChatEntry {role: 'assistant' | 'customer'; text: string; answer?: ChatAnswer; question?: string}
export interface ChatSession {owner: string; entries: ChatEntry[]; recentQuestions: string[]; lastProductIds: string[]; draft: string; suggestions: string[]; scroll: number; pending: AssistantRequest | null}
export interface LegacyRuntime {
  app: RuntimeState; ready: Promise<unknown>; catalogReady: Promise<Catalog>;
  request<T>(path: string, body?: unknown, method?: string): Promise<T>;
  cartView(): Cart; setCart(cart: Cart): void; updateState(): void;
  defaults(product: Product): Selections;
  calculated(product: Product, selections: Selections): {price: number; allergens: string[]; summary: string[]};
  toast(message: string): void;
}
export interface ReactStorefront {
  renderCatalog(): void; renderSpecial(tab?: string): void; renderSettings(settings: PublicSettings): void;
  configure(host: HTMLElement, product: Product, selections: Selections, changed: () => void): void;
}
declare global {interface Window {mhReact?: ReactStorefront}}
