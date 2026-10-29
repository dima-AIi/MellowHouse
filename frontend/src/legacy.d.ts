declare module '@mellow/core' {
  export const app: import('./types').RuntimeState;
  export const ready: Promise<unknown>;
  export const catalogReady: Promise<import('./types').Catalog>;
  export function request<T>(path: string,body?: unknown,method?: string): Promise<T>;
  export function cartView(): import('./types').Cart;
  export function setCart(cart: import('./types').Cart): void;
  export function updateState(): void;
  export function defaults(product: import('./types').Product): import('./types').Selections;
  export function calculated(product: import('./types').Product,selections: import('./types').Selections): {price: number; allergens: string[]; summary: string[]};
  export function toast(message: string): void;
}
declare module '@mellow/site' {}
declare module '@mellow/auth' {export function openAuth(after?:()=>void): void}
declare module '@mellow/images' {export function image(path: string, size?: number): string; export function imageSet(path: string): string}
declare module '@mellow/options' {
  export function optionLabel(group: import('./types').OptionGroup,option: import('./types').ProductOption): string;
  export function optionPrice(product: import('./types').Product,group: import('./types').OptionGroup,option: import('./types').ProductOption): string;
  export function optionHelp(group: import('./types').OptionGroup): string;
}
declare module '@mellow/chat-session' {
  export function readChat(storage: Storage): import('./types').ChatSession | null;
  export function writeChat(storage: Storage,session: import('./types').ChatSession): void;
}
