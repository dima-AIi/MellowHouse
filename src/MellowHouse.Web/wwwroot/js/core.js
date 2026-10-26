import{BrowserCart,browserCartKey}from'./browser-cart.js?v=20261026-v17';
import{image,imageSet}from'./product-images.js?v=20261026-v17';
import{syncChatOwner}from'./chat-session.js?v=20261026-v17';
import{readWhenReady,isJsonResponse}from'./server-connection.js?v=20261026-v17';
export const money=n=>new Intl.NumberFormat('ru-RU',{style:'currency',currency:'RUB',maximumFractionDigits:0}).format(n);
export const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
export const icon=name=>`<svg aria-hidden="true"><use href="/images/icons.svg#${name}"></use></svg>`;
export const app={state:null,catalog:null,method:localStorage.getItem('mh.method')==='delivery'?'delivery':'pickup'};
export class ApiError extends Error{constructor(message,status,field){super(message);this.status=status;this.field=field;}}
export async function request(path,body,method='GET'){
 let response;
 try{response=await fetch(path,{method,credentials:'same-origin',headers:{'Accept':'application/json',...(body!==undefined?{'Content-Type':'application/json'}:{}),...(method!=='GET'?{'X-CSRF-TOKEN':app.state?.csrfToken??''}:{})},...(body!==undefined?{body:JSON.stringify(body)}:{}),signal:AbortSignal.timeout(path==='/api/assistant'?55000:15000)});}
 catch{throw new ApiError('Не удалось связаться с кафе. Проверьте соединение и повторите. Корзина сохранена.',0);}
 if(!isJsonResponse(response))throw new ApiError('Кафе подключается. Меню доступно, подключение повторится автоматически.',response.ok?503:response.status);
 const data=await response.json();
 if(!response.ok)throw new ApiError(data.error||(response.status===401?'Войдите, чтобы продолжить. Корзина сохранена.':response.status===429?'Слишком много попыток. Подождите минуту и повторите.':'Не удалось выполнить действие. Обновите страницу и повторите.'),response.status,data.field);
 return data;
}
export async function refresh(){app.state=await request('/api/state');if(syncChatOwner(sessionStorage,app.state.authenticated?'u:'+app.state.profile.id:'guest'))document.dispatchEvent(new CustomEvent('mh:chat-reset'));if(!['pickup','delivery'].includes(localStorage.getItem('mh.method'))&&['pickup','delivery'].includes(app.state.fulfillmentMethod))app.method=app.state.fulfillmentMethod;updateState();return app.state;}
export function setCart(cart){if(app.state&&connected)app.state.cart=cart;updateState();document.dispatchEvent(new CustomEvent('mh:cart'));}
export function updateState(){
 const visibleCart=cartView();
 document.querySelectorAll('[data-open-cart]').forEach(el=>el.setAttribute('aria-label',`Открыть корзину, ${visibleCart.count} шт., ${money(visibleCart.subtotal)}`));
 document.querySelectorAll('[data-cart-total]').forEach(el=>el.textContent=money(visibleCart.subtotal));
 document.querySelectorAll('[data-cart-count]').forEach(el=>{el.textContent=visibleCart.count;el.hidden=visibleCart.count===0;});
 document.querySelectorAll('[data-product-count]').forEach(el=>{const count=visibleCart.items.filter(i=>i.product.id===el.dataset.productCount).reduce((sum,i)=>sum+i.quantity,0);el.textContent=el.hasAttribute('data-product-count-label')?'В корзине: '+count+' шт.':String(count);el.hidden=count===0;});
 document.querySelectorAll('[data-product-stepper]').forEach(el=>{const count=visibleCart.items.filter(i=>i.product.id===el.dataset.productStepper).reduce((sum,i)=>sum+i.quantity,0);el.classList.toggle('has-items',count>0);el.querySelector('.product-minus').hidden=count===0;});
 if(!app.state)return;
 document.querySelectorAll('[data-account-label]').forEach(el=>el.textContent=app.state.authenticated?'Профиль':'Войти');
 document.querySelectorAll('[data-account]').forEach(el=>el.setAttribute('aria-label',app.state.authenticated?'Профиль':'Войти'));
 document.querySelectorAll('[data-favorite]').forEach(el=>{const selected=app.state.favorites.includes(el.dataset.favorite);el.setAttribute('aria-pressed',String(selected));const p=app.catalog?.products.find(x=>x.id===el.dataset.favorite);el.setAttribute('aria-label',`${selected?'Убрать':'Добавить'} ${p?.name??'товар'} ${selected?'из избранного':'в избранное'}`);});
 document.querySelectorAll('[data-method]').forEach(el=>{el.classList.toggle('active',el.dataset.method===app.method);el.setAttribute('aria-pressed',String(el.dataset.method===app.method));});
 document.querySelectorAll('[data-admin-link]').forEach(el=>el.hidden=app.state.profile?.role!=='Admin');
 document.querySelectorAll('[data-logout]').forEach(el=>el.hidden=!app.state.authenticated);
}
let toastTimer;export function toast(message,undo){
 const host=document.querySelector('#toast');const modal=[...document.querySelectorAll('dialog[open]')].at(-1);(modal??document.body).append(host);host.querySelector('span').textContent=message;const button=host.querySelector('button');button.hidden=!undo;button.onclick=undo?async()=>{button.disabled=true;try{await undo();host.hidden=true;}catch(e){toast(e.message);}finally{button.disabled=false;}}:null;host.hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>host.hidden=true,undo?10000:4200);
}
export async function busy(button,operation){if(button?.disabled)return;const old=button?.innerHTML;if(button){button.disabled=true;button.setAttribute('aria-busy','true');}try{return await operation();}finally{if(button){button.disabled=false;button.removeAttribute('aria-busy');if(old!==undefined&&button.dataset.loadingLabel==='true')button.innerHTML=old;}}}
export function handle(selector,callback){document.addEventListener('click',async e=>{const b=e.target.closest(selector);if(!b)return;try{await busy(b,()=>callback(b,e));}catch(error){toast(error.message);}});}
export const product=id=>app.catalog?.products.find(p=>p.id===id);
export const defaults=p=>Object.fromEntries(p.groups.map(g=>[g.id,[...g.defaults]]));
export function calculated(p,selection){let price=p.price;let allergens=new Set(p.allergens);let summary=[];for(const g of p.groups)for(const id of selection[g.id]??[]){const o=g.options.find(o=>o.id===id);if(!o)continue;price+=o.price;summary.push(o.label);for(const a of o.removeAllergens??[])allergens.delete(a);for(const a of o.addAllergens??[])allergens.add(a);}return{price,allergens:[...allergens],summary};}
export function configure(host,p,selection,onChange){
 const adjusted=n=>n>0?'+'+money(n):money(n);
 host.innerHTML=p.groups.map(g=>`<fieldset><legend>${esc(g.label)}</legend><div class="${g.multiple?'option-checks':'option-grid'}" data-option-grid="${esc(g.id)}">${g.options.map(o=>{const selected=(selection[g.id]??[]).includes(o.id);const label=g.id==='size'?money(p.price+o.price):o.price?`+${money(o.price)}`:'0 ₽';return g.multiple?`<label class="option-check"><input type="checkbox" name="${host.id}-${esc(g.id)}" value="${esc(o.id)}" data-group="${esc(g.id)}" ${selected?'checked':''}/><span>${esc(o.label)}</span><small>${o.price?`+${money(o.price)}`:'0 ₽'}</small></label>`:`<label class="option-tile"><input type="radio" name="${host.id}-${esc(g.id)}" value="${esc(o.id)}" data-group="${esc(g.id)}" ${selected?'checked':''}/><span>${esc(o.label)}</span><small>${label}</small></label>`;}).join('')}</div></fieldset>`).join('');
 host.querySelectorAll('.option-grid').forEach(el=>el.style.setProperty('--columns',String(Math.min(3,p.groups.find(g=>g.id===el.dataset.optionGrid).options.length))));
 for(const g of p.groups){const grid=host.querySelector(`[data-option-grid="${CSS.escape(g.id)}"]`);const min=Math.max(g.min??0,g.required?1:0);if(!g.multiple&&min===0){const label=document.createElement('label');label.className='option-tile';label.innerHTML=`<input type="radio" name="${host.id}-${esc(g.id)}" value="" data-group="${esc(g.id)}" ${selection[g.id]?.length?'':'checked'}/><span>Без добавления</span><small>0 ₽</small>`;grid.prepend(label);}if(g.multiple){const note=document.createElement('p');note.className='small muted';note.textContent=`Выберите от ${min} до ${g.max??g.options.length}`;grid.before(note);}grid.querySelectorAll('small').forEach((small,i)=>{const input=small.closest('label').querySelector('input');const o=g.options.find(o=>o.id===input.value);if(o&&g.id!=='size')small.textContent=adjusted(o.price);});}
 host.setAttribute('aria-busy','false');host.onchange=e=>{const g=p.groups.find(x=>x.id===e.target.dataset.group);if(!g)return;const chosen=[...host.querySelectorAll(`input[data-group="${CSS.escape(g.id)}"]:checked`)].map(x=>x.value).filter(Boolean);const min=Math.max(g.min??0,g.required?1:0),max=g.max??(g.multiple?g.options.length:1);if(chosen.length<min||chosen.length>max){e.target.checked=!e.target.checked;toast(`В группе «${g.label}» выберите от ${min} до ${max}.`);return;}selection[g.id]=chosen;onChange();};onChange();
}
export function card(p){const count=cartView().items.filter(i=>i.product.id===p.id).reduce((sum,i)=>sum+i.quantity,0);return `<article class="product-card" data-product="${esc(p.id)}"><a class="product-photo" href="/Product/${esc(p.id)}" data-product-link><img src="${esc(image(p.image))}" srcset="${esc(imageSet(p.image))}" sizes="(max-width:700px) 46vw, (max-width:1100px) 30vw, 320px" alt="${esc(p.name)}" width="640" height="426" loading="lazy"/></a><div class="card-copy"><h3><a href="/Product/${esc(p.id)}" data-product-link>${esc(p.name)}</a></h3><p class="card-description">${esc(p.description)}</p><p class="portion">${esc(p.portion)}</p><div class="card-actions"><strong>${money(p.price)}</strong><button class="icon-btn favorite" data-favorite="${esc(p.id)}" aria-label="Добавить ${esc(p.name)} в избранное" aria-pressed="false">${icon('heart')}</button><div class="add-control" data-product-stepper="${esc(p.id)}"><button class="round-btn product-minus" data-product-delta="-1" data-cart-product="${esc(p.id)}" aria-label="Убрать одну порцию: ${esc(p.name)}" ${count?'':'hidden'}>−</button><output class="product-cart-count" data-product-count="${esc(p.id)}" aria-label="В корзине: ${esc(p.name)}" ${count?'':'hidden'}>${count}</output><button class="round-btn" data-cart-product="${esc(p.id)}" data-product-delta="1" ${p.available?'':'disabled title="Сейчас недоступно"'} aria-label="Добавить ${esc(p.name)} в корзину">+</button></div></div></div></article>`;}
export function totals(subtotal,delivery){return `<div class="total-row"><span>Товары</span><strong>${money(subtotal)}</strong></div><div class="total-row"><span>${delivery?'Доставка':'Самовывоз'}</span><strong>${delivery?money(delivery):'Бесплатно'}</strong></div><div class="total-row grand"><span>Итого</span><strong>${money(subtotal+delivery)}</strong></div>`;}
export function snapshotItems(items){return items.map(i=>`<div class="summary-line"><img src="${esc(image(i.image))}" alt="${esc(i.name)}" width="58" height="60"/><div><h3>${esc(i.name)}</h3><p>${esc(i.summary.join(', ')||i.portion)}</p><span>${i.quantity} шт.</span></div><strong>${money(i.total)}</strong></div>`).join('');}
const browserCart=new BrowserCart(localStorage,{onChange:()=>{updateState();document.dispatchEvent(new CustomEvent('mh:cart'));}});
export function cartView(){return connected?app.state.cart:browserCart.view(app.catalog?.products);}
export function cartConnected(){return connected;}
export function cartNotice(){
 if(!connected)return browserCart.read()?.frozen?'Сверяем корзину с кафе. После проверки цен можно будет оформить заказ.':'Выбирайте блюда — корзина сохраняется в этом браузере. Суммы предварительные; цены и сохранённую на сервере корзину проверим после подключения.';
 try{return JSON.parse(sessionStorage.getItem('mh.cart-sync-notice')??'[]').join(' ');}catch{return '';}
}
export function dismissCartNotice(){sessionStorage.removeItem('mh.cart-sync-notice');document.dispatchEvent(new CustomEvent('mh:cart'));}
export function clearBrowserCart(){localStorage.removeItem(browserCartKey);sessionStorage.removeItem('mh.cart-sync-notice');}
export async function addCart(change){await catalogReady;if(!connected){browserCart.add(product(change.productId),change.selections,change.quantity??1);return cartView();}return request('/api/cart',change,'POST');}
export async function editCart(id,change){await catalogReady;if(!connected){const line=cartView().items.find(l=>l.id===id);if(!line)throw new Error('Позиция изменилась. Откройте корзину заново.');browserCart.edit(id,line.product,change.selections,change.quantity);return cartView();}if(!app.state.cart.items.some(l=>l.id===id))throw new Error('Корзина уже сверена с кафе. Закройте это окно и откройте позицию заново, чтобы проверить количество.');return request('/api/cart/'+id,change,'PUT');}
export async function removeCart(id){await catalogReady;if(!connected){if(id)browserCart.remove(id);else browserCart.clear();return cartView();}return request('/api/cart'+(id?'/'+id:''),undefined,'DELETE');}
export async function adjustCart(id,delta){await catalogReady;if(!connected){const lines=cartView().items.filter(l=>l.product.id===id);if(lines.length>1){const e=new Error('В корзине несколько вариантов. Выберите нужный вариант в корзине.');e.field='configuration';throw e;}if(!lines.length){if(delta>0)return addCart({productId:id,quantity:1});return cartView();}const line=lines[0],quantity=line.quantity+delta;return quantity<1?removeCart(line.id):editCart(line.id,{quantity,selections:line.selections});}return request('/api/cart/product/'+encodeURIComponent(id),{delta},'POST');}
window.addEventListener('storage',e=>{if(e.key===browserCartKey){updateState();document.dispatchEvent(new CustomEvent('mh:cart'));if(connected&&!e.newValue)request('/api/cart').then(setCart).catch(()=>{});}});
const staticStorefront=document.documentElement.hasAttribute('data-static-storefront');
function setCatalog(c){const changed=JSON.stringify(app.catalog)!==JSON.stringify(c);app.catalog=c;if(changed)document.dispatchEvent(new CustomEvent('mh:catalog'));return c;}
let status,waitingTimer,connected=false;
function connectionStatus(failed=false){
 if(!status){status=document.createElement('div');status.className='server-connection';status.setAttribute('role','status');status.setAttribute('aria-live','polite');status.innerHTML='<span></span><button type="button" hidden>Повторить</button>';status.querySelector('button').onclick=()=>location.reload();document.body.append(status);}
 status.hidden=false;status.querySelector('span').textContent=failed?'Пока нет связи с кафе. Выбранные блюда сохранены в браузере. Повторите подключение перед оформлением.':'Выбирайте блюда — корзина работает. Подключаем кафе, чтобы сверить цены и оформить заказ.';status.querySelector('button').hidden=!failed;
}
waitingTimer=setTimeout(()=>connectionStatus(),1800);
const stateReady=readWhenReady(()=>refresh(),{onWaiting:()=>connectionStatus()});
export const catalogReady=(staticStorefront?request('/catalog.json'):stateReady.then(()=>readWhenReady(()=>request('/api/catalog')))).then(setCatalog);
export const ready=(async()=>{
 try{
  // Establish the guest/account cookie first. Parallel state/catalog requests can otherwise create different guest owners.
  await stateReady;
  const catalog=staticStorefront?await readWhenReady(()=>request('/api/catalog'),{onWaiting:()=>connectionStatus()}):await catalogReady;
  await catalogReady;setCatalog(catalog);
  const batch=browserCart.freeze();
  if(batch){
   const result=await readWhenReady(()=>request('/api/cart/browser-sync',{requestKey:batch.id,lines:batch.lines.map(l=>({productId:l.productId,selections:l.selections,quantity:l.quantity,expectedUnitPrice:l.expectedUnitPrice}))},'POST'),{onWaiting:()=>connectionStatus()});
   app.state.cart=result.cart;sessionStorage.setItem('mh.cart-sync-notice',JSON.stringify(result.warnings));
  }
  if(!batch)app.state.cart=await readWhenReady(()=>request('/api/cart'),{onWaiting:()=>connectionStatus()});
  connected=true;if(batch)browserCart.acknowledge(batch.id);updateState();document.dispatchEvent(new CustomEvent('mh:cart'));document.dispatchEvent(new CustomEvent('mh:connected'));return app;
 }catch(error){connectionStatus(true);throw error;}
 finally{clearTimeout(waitingTimer);if(connected&&status)status.hidden=true;}
})();
