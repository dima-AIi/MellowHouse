import test from 'node:test';
import assert from 'node:assert/strict';
import {sanitizePage,deploymentRoutes,origin} from '../deploy/vercel-proxy/build.mjs';
import {readWhenReady,isJsonResponse} from '../src/MellowHouse.Web/wwwroot/js/server-connection.js';

test('Render loading/error pages cannot become the static homepage',()=>{
 for(const html of ['<html><body>Render SERVICE WAKING UP</body></html>','<html><body>Bad Gateway</body></html>'])assert.throws(()=>sanitizePage(html));
});
test('static snapshots contain no visitor-specific antiforgery form tokens',()=>{
 const html='<html lang="ru"><title>Mellow House</title><main id="main"><form><input name="__RequestVerificationToken" type="hidden" value="visitor-secret" /><input type="hidden" name="__RequestVerificationToken" value="other-secret"></form></main></html>';
 const clean=sanitizePage(html);assert.match(clean,/data-static-storefront="true"/);assert.doesNotMatch(clean,/visitor-secret|other-secret|__RequestVerificationToken/);
});
test('only APIs/admin and unknown routes go to Render; storefront routes and assets are local first',()=>{
 const routes=deploymentRoutes(['syrniki']);const match=path=>routes.find(r=>r.dest&&new RegExp('^'+r.src+'$','i').test(path));
 for(const path of ['/','/Menu','/Account','/Checkout','/Product/syrniki','/Product/new-item','/Payment/00000000-0000-0000-0000-000000000000'])assert.ok(match(path).dest.startsWith('/pages/'),path);
 for(const path of ['/api/state','/api/orders','/api/catalog','/Admin','/Admin/Login','/health']){const route=match(path);assert.ok(route.dest.startsWith(origin));assert.equal(route.headers['Vercel-CDN-Cache-Control'],'no-store');}
 assert.ok(routes.findIndex(r=>r.handle==='filesystem')<routes.findIndex(r=>r.src==='/(.*)'&&r.dest));
});
test('cold-start HTML is never treated as successful JSON',()=>{
 assert.equal(isJsonResponse(new Response('<html>Render</html>',{headers:{'Content-Type':'text/html'}})),false);
 assert.equal(isJsonResponse(new Response('{}',{headers:{'Content-Type':'application/json; charset=utf-8'}})),true);
});
test('safe startup reads recover from cold-start responses and network timeouts',async()=>{
 let time=0,tries=0,waiting=0;
 const result=await readWhenReady(async()=>{if(++tries<4)throw Object.assign(new Error('waking'),{status:503});return {authenticated:true,cart:{count:3}};},{now:()=>time,sleep:async ms=>{time+=ms;},onWaiting:()=>waiting++});
 assert.equal(tries,4);assert.equal(waiting,3);assert.equal(result.cart.count,3);
});
test('a genuine authentication or validation rejection is not retried',async()=>{
 let tries=0;await assert.rejects(readWhenReady(async()=>{tries++;throw Object.assign(new Error('denied'),{status:401});}),/denied/);assert.equal(tries,1);
});
test('offline startup is bounded rather than retrying indefinitely',async()=>{
 let time=0,tries=0;await assert.rejects(readWhenReady(async()=>{tries++;throw Object.assign(new Error('offline'),{status:0});},{timeout:5000,delay:2500,now:()=>time,sleep:async ms=>{time+=ms;}}),/offline/);assert.equal(time,5000);assert.equal(tries,2);
});
