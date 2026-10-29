import type { Category, Product, Order, PublicSettings } from "./types";
export interface AdminProduct extends Product {
  draft: boolean;
  archived: boolean;
  sortOrder: number;
  cost: number | null;
  serving: number | null;
  unit: string;
}
export interface AdminCategory extends Category {
  visible: boolean;
  archived: boolean;
}
export interface AdminCatalog {
  products: AdminProduct[];
  categories: AdminCategory[];
}
export interface Client {
  id: string;
  name: string;
  contact: string;
  role: string;
  blocked: boolean;
  blockReason: string;
  sample: boolean;
  active?: boolean;
  orders?: number;
  hasPassword: boolean;
  createdUtc: string;
  lastLoginUtc?: string;
  lastActiveUtc?: string;
  authMethod?: string;
}
export interface Expense {
  id: string;
  date: string;
  category: string;
  amount: number;
  note: string;
  source?: string;
}
export interface Report {
  from: string;
  to: string;
  basis: string;
  collected: number;
  totalKnownExpenses: number;
  provisionalResult: number;
  netProfit: number | null;
  created: number;
  receipts: number;
  newOrders: number;
  preparing: number;
  ready: number;
  completed: number;
  rejected: number;
  gross: number;
  refunds: number;
  delivery: number;
  average: number | null;
  knownCost: number;
  missingCost: boolean;
  operatingExpenses: number;
  counterRevenue: number;
  webRevenue: number;
  products: {
    id: string;
    name: string;
    quantity: number;
    revenue: number;
    knownCost: number;
    missingCost: boolean;
  }[];
  expenses: Expense[];
}
export interface ClientDetail {
  profile: Client;
  orders: Order[];
}
export interface AdminSettings {
  site: PublicSettings;
  paymentProvider: string;
  assistantProvider: string;
  assistantModel: string;
  assistantModels: string[];
  assistantLastModel?: string;
}
export interface AuditRow {
  id: number;
  action: string;
  target: string;
  detail: string;
  createdUtc: string;
  actor: string;
}
