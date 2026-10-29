import {defineConfig} from 'vitest/config';
import {fileURLToPath} from 'node:url';

export default defineConfig({
  base: '/react/',
  resolve: {alias: {...Object.fromEntries([['images','product-images'],['options','product-options'],['chat-session','chat-session']].map(([alias,name])=>[
    `@mellow/${alias}`,fileURLToPath(new URL(`../src/MellowHouse.Web/wwwroot/js/${name}.js`,import.meta.url))
  ])),...(process.env.VITEST?{'@mellow/auth':fileURLToPath(new URL('tests/auth-stub.ts',import.meta.url))}:{})}},
  build: {
    outDir: '../src/MellowHouse.Web/wwwroot/react', emptyOutDir: true,
    target: 'es2022', manifest: true,
    rollupOptions: {
      input: 'src/main.tsx',
      external: ['@mellow/core','@mellow/site','@mellow/auth'],
      output: {
        entryFileNames: 'storefront.js', chunkFileNames: 'chunks/[name]-[hash].js',
        paths: {'@mellow/core':'/js/core.js?v=20261028-v20','@mellow/site':'/js/site.js?v=20261029-react25','@mellow/auth':'/js/cart-auth.js?v=20261028-v20'}
      }
    }
  },
  test: {environment: 'jsdom', setupFiles: ['tests/setup.ts'], restoreMocks: true}
});
