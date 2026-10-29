import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile,readdir} from 'node:fs/promises';
const read=path=>readFile(new URL('../'+path,import.meta.url),'utf8');
test('public and admin layouts load the full React applications without legacy controllers',async()=>{
 const publicLayout=await read('src/MellowHouse.Web/Pages/Shared/_Layout.cshtml');
 const adminLayout=await read('src/MellowHouse.Web/Pages/Shared/_AdminLayout.cshtml');
 const bootstrap=await read('src/MellowHouse.Web/wwwroot/js/react-bootstrap.js');
 assert.match(publicLayout,/data-storefront-version="26"/);
 assert.match(adminLayout,/\/react\/admin\.js/);
 assert.match(bootstrap,/\/react\/storefront\.js/);
 for(const content of [publicLayout,adminLayout,bootstrap])assert.doesNotMatch(content,/\/js\/(?:site|core|admin|auth)\.js/);
});
test('production bundles have no imports of the former runtime',async()=>{
 const root=new URL('../src/MellowHouse.Web/wwwroot/react/',import.meta.url);
 const files=(await readdir(root,{recursive:true})).filter(file=>file.endsWith('.js'));
 assert.ok(files.includes('storefront.js')&&files.includes('admin.js')&&files.includes('welcome.js'));
 for(const file of files){const source=await readFile(new URL(file.replaceAll('\\','/'),root),'utf8');assert.doesNotMatch(source,/@mellow\/|from["']\/js\/|import\(["']\/js\//);}
});
test('cache and static export share the full application version',async()=>{
 assert.match(await read('src/MellowHouse.Web/wwwroot/sw.js'),/v26/);
 const build=await read('deploy/vercel-proxy/build.mjs');
 assert.match(build,/version:26/);assert.match(build,/react-typescript-static-vercel-api-render/);
});
