import {cp, mkdir, readFile, writeFile, readdir, rm} from 'node:fs/promises';
import {resolve, dirname, join, relative} from 'node:path';
import {fileURLToPath, pathToFileURL} from 'node:url';

const here=dirname(fileURLToPath(import.meta.url));
export const origin='https://mellow-house.onrender.com';
const security={
 'X-Content-Type-Options':'nosniff',
 'Referrer-Policy':'strict-origin-when-cross-origin',
 'Content-Security-Policy':"default-src 'self'; img-src 'self' data:; style-src 'self'; font-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
};
export const pages=[['/','index'],['/Menu','menu'],['/About','about'],['/Contacts','contacts'],['/Delivery','delivery'],['/Allergens','allergens'],['/Legal','legal'],['/App','app'],['/Account','account'],['/Checkout','checkout'],['/ResetPassword','reset-password'],['/Payment/00000000-0000-0000-0000-000000000000','payment'],['/Order/00000000-0000-0000-0000-000000000000','order']];
export function sanitizePage(html){
 if(!/<html[\s>]/i.test(html)||!html.includes('id="main"')||!html.includes('Mellow House'))throw new Error('Origin returned a loading/error page instead of the cafe.');
 const clean=html.replace(/<input\b[^>]*\bname=["']__RequestVerificationToken["'][^>]*\/?\s*>/gi,'').replace(/<html\b/i,'<html data-static-storefront="true"');
 if(clean.includes('__RequestVerificationToken'))throw new Error('A session token must never be exported.');
 return clean;
}
export function deploymentRoutes(productIds){
 const proxyHeaders={'Cache-Control':'private, no-store, max-age=0','CDN-Cache-Control':'no-store','Vercel-CDN-Cache-Control':'no-store'};
 const routes=[{src:'/(.*)',headers:security,continue:true},{src:'/Privacy/?',status:308,headers:{Location:'/Legal#privacy'}},
  {src:'/(api(?:/.*)?|Admin(?:/.*)?|health)',dest:origin+'/$1',headers:proxyHeaders},
  ...pages.filter(([,name])=>!['payment','order'].includes(name)).map(([path,name])=>({src:path==='/'?'/':path+'/?',dest:'/pages/'+name+'.html'})),
  {src:'/Payment/[0-9a-fA-F-]{36}/?',dest:'/pages/payment.html'},
  {src:'/Order/[0-9a-fA-F-]{36}/?',dest:'/pages/order.html'},
  ...productIds.map(id=>({src:'/Product/'+id+'/?',dest:'/pages/products/'+id+'.html'})),
  {src:'/Product/[^/]+/?',dest:'/pages/product.html'},
  {handle:'filesystem'},
  {src:'/(.*)',dest:origin+'/$1',headers:proxyHeaders}];
 return routes;
}
async function get(path,kind='json'){
 const deadline=Date.now()+180000;let last;
 while(Date.now()<deadline){
  try{
   const response=await fetch(origin+path,{headers:{Accept:kind==='json'?'application/json':'text/html'},signal:AbortSignal.timeout(25000),redirect:'error'});
   const contentType=response.headers.get('content-type')??'';
   if(!response.ok)throw new Error(path+': HTTP '+response.status);
   if(kind==='json'){if(!contentType.includes('application/json'))throw new Error('Origin is waking up');return await response.json();}
   const html=sanitizePage(await response.text());
   if(!html.includes('data-storefront-version="23"'))throw new Error('Waiting for restored home layout on Render');
   return html;
  }catch(error){last=error;await new Promise(resolve=>setTimeout(resolve,2500));}
 }
 throw new Error('Cannot export '+path+': '+last?.message);
}
async function pool(items,worker){let next=0;await Promise.all(Array.from({length:4},async()=>{while(next<items.length){const item=items[next++];await worker(item);}}));}
export async function build(){
 const output=join(here,'.vercel/output'),assets=join(output,'static');
 // Only this generated directory is ever replaced. Never export a browser session or an admin page.
 if(relative(here,output)!==join('.vercel','output'))throw new Error('Invalid build directory');
 await rm(output,{recursive:true,force:true});await mkdir(join(assets,'pages/products'),{recursive:true});
 const wwwroot=resolve(here,'../../src/MellowHouse.Web/wwwroot');
 for(const entry of await readdir(wwwroot,{withFileTypes:true})){
  if(entry.name==='uploads')continue;
  await cp(join(wwwroot,entry.name),join(assets,entry.name),{recursive:true});
 }
 const catalog=await get('/api/catalog');
 if(!Array.isArray(catalog.products)||!catalog.products.length||!Array.isArray(catalog.categories))throw new Error('Invalid public catalog');
 if(catalog.products.some(p=>!/^[-a-zA-Z0-9_]+$/.test(p.id)))throw new Error('Unsafe product ID');
 const state=await get('/api/state');
 if(state.authenticated||state.profile||state.cart?.count)throw new Error('Export must use an anonymous, empty session');
 // Explicit whitelist: state, cookies, profile, cart and CSRF tokens are never written to the deployment.
 await writeFile(join(assets,'catalog.json'),JSON.stringify(catalog));
 await writeFile(join(assets,'site-public.json'),JSON.stringify(state.settings));
 await pool(pages,async([path,name])=>writeFile(join(assets,'pages',name+'.html'),await get(path,'html')));
 await pool(catalog.products,async p=>writeFile(join(assets,'pages/products',p.id+'.html'),await get('/Product/'+p.id,'html')));
 const sample=await readFile(join(assets,'pages/products',catalog.products[0].id+'.html'),'utf8');
 const start=sample.indexOf('<main id="main">'),end=sample.indexOf('</main>',start);
 await writeFile(join(assets,'pages/product.html'),sample.slice(0,start)+'<main id="main"><div class="container page-space" id="dynamic-product"><p>Загружаем товар…</p></div>'+sample.slice(end));
 const media=[...new Set([...catalog.products.map(p=>p.image),...catalog.categories.map(c=>c.image)].filter(p=>/^\/media\/[-a-zA-Z0-9_.]+$/.test(p)))];
 await mkdir(join(assets,'media'),{recursive:true});
 await pool(media,async path=>{const r=await fetch(origin+path,{signal:AbortSignal.timeout(30000)});if(!r.ok||!r.headers.get('content-type')?.startsWith('image/'))throw new Error('Missing public image: '+path);await writeFile(join(assets,path.slice(1)),new Uint8Array(await r.arrayBuffer()));});
 await writeFile(join(assets,'storefront-build.json'),JSON.stringify({version:23,revision:process.env.VERCEL_GIT_COMMIT_SHA??'local',builtAt:new Date().toISOString(),products:catalog.products.length,architecture:'static-vercel-api-render'}));
 await writeFile(join(output,'config.json'),JSON.stringify({version:3,routes:deploymentRoutes(catalog.products.map(p=>p.id))},null,2));
 console.log(`Static storefront: ${pages.length+catalog.products.length+1} pages, ${catalog.products.length} products. Private routes stay uncached on Render.`);
}
if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href)await build();
