export interface ProductOption {
  id: string;
  label: string;
  price: number;
  addAllergens?: string[];
  removeAllergens?: string[];
  cost?: number | null;
}
export interface OptionGroup {
  id: string;
  label: string;
  options: ProductOption[];
  defaults: string[];
  multiple: boolean;
  required: boolean;
  min?: number | null;
  max?: number | null;
}
export type Selections = Record<string, string[]>;
export interface NutritionFacts {
  calories: number;
  protein: number;
  fat: number;
  carbs: number;
  estimated: boolean;
}
export interface Product {
  id: string;
  categoryId: string;
  name: string;
  price: number;
  portion: string;
  description: string;
  ingredients: string;
  image: string;
  available: boolean;
  popular: boolean;
  recommended: boolean;
  groups: OptionGroup[];
  allergens: string[];
  nutrition?: string;
  nutritionFacts?: NutritionFacts | null;
}
export interface Category {
  id: string;
  name: string;
  image: string;
  onHome: boolean;
  sortOrder: number;
}
export interface Catalog {
  products: Product[];
  categories: Category[];
}
export interface CartLine {
  available?: boolean;
  id: string;
  product: Product;
  quantity: number;
  selections: Selections;
  unitPrice: number;
  total: number;
  summary: string[];
}
export interface Cart {
  items: CartLine[];
  count: number;
  subtotal: number;
  revision?: number;
}
export interface PublicSettings {
  marketingTitle: string;
  menuTitle: string;
  menuText: string;
  faq: { question: string; answer: string }[];
  name?: string;
  cafeName?: string;
  phone?: string;
  email?: string;
  address?: string;
  hours?: string;
  openingTime?: string;
  closingTime?: string;
  closedWeekdays?: number[];
  closedDates?: string[];
  deliveryCities?: string[];
  deliveryPrice?: number;
  paymentMethods?: string[];
  video?: string;
  poster?: string;
}
export interface SessionState {
  csrfToken: string;
  authenticated: boolean;
  profile: {
    id: string;
    role: string;
    name: string;
    contact?: string;
    birthDate?: string;
    deliveryPhone?: string;
    hasPassword?: boolean;
  } | null;
  favorites: string[];
  cart: Cart;
  settings: PublicSettings;
  deliveryPrice?: number;
  fulfillmentMethod?: string;
}
export interface RuntimeState {
  state: SessionState | null;
  catalog: Catalog | null;
  method: "pickup" | "delivery";
}
export interface AssistantRequest {
  message: string;
  recentQuestions: string[];
  lastProductIds: string[];
  requestId: string;
}
export interface AssistantAnswer {
  text: string;
  productIds: string[];
  suggestions: string[];
  cart?: Cart;
  addedProductIds?: string[];
  requiresLogin?: boolean;
}
export interface ChatAnswer {
  productIds: string[];
  suggestions?: string[];
  addedProductIds?: string[];
  requiresLogin?: boolean;
  cartTotal?: number;
}
export interface ChatEntry {
  role: "assistant" | "customer";
  text: string;
  answer?: ChatAnswer;
  question?: string;
}
export interface ChatSession {
  owner: string;
  entries: ChatEntry[];
  recentQuestions: string[];
  lastProductIds: string[];
  draft: string;
  suggestions: string[];
  scroll: number;
  pending: AssistantRequest | null;
}
export interface ShopRuntime {
  app: RuntimeState;
  ready: Promise<unknown>;
  catalogReady: Promise<Catalog>;
  request<T>(path: string, body?: unknown, method?: string): Promise<T>;
  cartView(): Cart;
  setCart(cart: Cart): void;
  updateState(): void;
  defaults(product: Product): Selections;
  calculated(
    product: Product,
    selections: Selections,
  ): { price: number; allergens: string[]; summary: string[] };
  toast(message: string): void;
  openAuth?(after?: () => void): void;
}
export interface CheckoutData {
  requestKey: string;
  method: "pickup" | "delivery";
  city: string;
  street: string;
  apartment: string;
  name: string;
  phone: string;
  cafe: string;
  timing: "asap" | "scheduled";
  scheduledLocal: string;
  comment: string;
  paymentMethod: string;
  consent: boolean;
  signature?: string | null;
}
export interface OrderItem {
  productId: string;
  name: string;
  image: string;
  portion: string;
  configuration: string;
  summary: string[];
  quantity: number;
  unitPrice: number;
  total: number;
}
export interface Order {
  id: string;
  number: string;
  status: string;
  items: OrderItem[];
  checkout: CheckoutData;
  subtotal: number;
  delivery: number;
  total: number;
  createdUtc: string;
  paymentMethod: string;
  fulfillment: string;
  refunded: number;
  source: string;
  allowedTransitions: string[];
  guest: boolean;
  guestAccessToken?: string;
  canCancel: boolean;
  events?: { status: string; note: string; createdUtc: string }[];
  refunds?: {
    id: string;
    amount: number;
    delivery?: number;
    lines?: { index: number; quantity: number }[];
    reason: string;
    createdUtc: string;
  }[];
}
export type JsonValue =
  | string
  | number
  | boolean
  | null
  | JsonValue[]
  | { [key: string]: JsonValue };
export type JsonRecord = Record<string, JsonValue>;
