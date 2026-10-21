import{app,ready,request,refresh,setCart,updateState,money,esc,product,defaults,calculated,configure,card,toast,handle}from'./core.js?v=20261021-v9';
import'./cart-auth.js?v=20261021-v9';
import{bindPhones,bindPasswordButtons}from'./forms.js?v=20261021-v9';
bindPhones();bindPasswordButtons();
import'./experience.js?v=20261021-v9';
document.documentElement.classList.add('js-ready');
const reduce=matchMedia('(prefers-reduced-motion: reduce)');
const menuButton=document.querySelector('#mobile-menu-button'),mobileNav=document.querySelector('#mobile-nav');
menuButton.addEventListener('click',()=>{const open=mobileNav.hidden;mobileNav.hidden=!open;menuButton.setAttribute('aria-expanded',String(open));menuButton.setAttribute('aria-label',open?'Закрыть навигацию':'Открыть навигацию');});
document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!mobileNav.hidden){mobileNav.hidden=true;menuButton.setAttribute('aria-expanded','false');menuButton.focus();}});
mobileNav.addEventListener('click',e=>{if(e.target.closest('a')){mobileNav.hidden=true;menuButton.setAttribute('aria-expanded','false');}});
document.querySelectorAll('.desktop-nav a').forEach(a=>{if(new URL(a.href).pathname===location.pathname)a.setAttribute('aria-current','page');});
const currentTab=document.querySelector('[data-menu-tab]')?.dataset.menuTab;
document.querySelectorAll('[data-tab]').forEach(a=>{if(a.dataset.tab===(currentTab??(location.pathname==='/Account'&&!['profile','address'].includes(new URLSearchParams(location.search).get('view'))?'history':'none'))){a.classList.add('active');a.setAttribute('aria-current','page');}});
const reveal=document.querySelector('.reveal-message');
if(reveal){
 const parts=[...reveal.querySelectorAll('h2,p')];
 const showAll=()=>{reveal.classList.add('visible');parts.forEach(part=>part.classList.add('is-in-view'));};
 if(reduce.matches||document.documentElement.classList.contains('mh-welcome-return')||!('IntersectionObserver'in window))showAll();
 else{
  const observer=new IntersectionObserver(entries=>{
   for(const entry of entries)entry.target.classList.toggle('is-in-view',entry.isIntersecting);
   reveal.classList.toggle('visible',parts.some(part=>part.classList.contains('is-in-view')));
  },{threshold:.12,rootMargin:'0px 0px -8% 0px'});
  parts.forEach(part=>observer.observe(part));
  reduce.addEventListener('change',e=>{if(e.matches){observer.disconnect();showAll();}});
 }
}
const heroImage=document.querySelector('.hero-image');let heroFrame=false;
if(heroImage&&!reduce.matches){const hero=document.querySelector('.hero');const animateHero=()=>{heroFrame=false;const y=window.scrollY;if(y<hero.offsetHeight+100){heroImage.style.transform=`translateY(${Math.min(50,y*.12)}px) scale(1.03)`;heroImage.style.opacity=String(Math.max(.65,1-y/hero.offsetHeight*.32));}};window.addEventListener('scroll',()=>{if(!heroFrame){heroFrame=true;requestAnimationFrame(animateHero);}},{passive:true});animateHero();}
const track=document.querySelector('#popular-track');
function carouselState(){if(!track)return;const max=track.scrollWidth-track.clientWidth;document.querySelector('[data-carousel=prev]').disabled=track.scrollLeft<2;document.querySelector('[data-carousel=next]').disabled=track.scrollLeft>=max-2;}
if(track){track.addEventListener('scroll',carouselState,{passive:true});window.addEventListener('resize',carouselState);carouselState();}
handle('[data-carousel]',b=>{track.scrollBy({left:(b.dataset.carousel==='next'?1:-1)*track.clientWidth*.7,behavior:reduce.matches?'instant':'smooth'});});

const sections=[...document.querySelectorAll('[data-section]')],categoryLinks=[...document.querySelectorAll('[data-category]')];let scrollAnimation=0,activeId='',scrollScheduled=false;
function offset(){return document.querySelector('.site-header').offsetHeight+(innerWidth<=700?document.querySelector('.category-nav')?.offsetHeight??0:document.querySelector('.catalog-toolbar')?.offsetHeight??0)+16;}
function activateCategory(id){if(activeId===id)return;activeId=id;for(const a of categoryLinks){const active=a.dataset.category===id;a.classList.toggle('active',active);if(active)a.setAttribute('aria-current','location');else a.removeAttribute('aria-current');}if(innerWidth<=700){const a=categoryLinks.find(a=>a.dataset.category===id),nav=document.querySelector('.category-nav');if(a&&nav){const left=a.offsetLeft-nav.clientWidth/2+a.offsetWidth/2;nav.scrollTo({left,behavior:reduce.matches?'instant':'smooth'});}}}
function updateActive(){scrollScheduled=false;if(currentTab!=='menu'||!sections.length)return;let selected=sections[0].id;for(const s of sections){if(s.getBoundingClientRect().top<=offset()+40)selected=s.id;else break;}activateCategory(selected);}
function stopScroll(){cancelAnimationFrame(scrollAnimation);scrollAnimation=0;}
function jumpCategory(id,instant=false){const target=document.getElementById(id);if(!target)return;stopScroll();const start=scrollY,end=Math.max(0,scrollY+target.getBoundingClientRect().top-offset()),distance=end-start;activateCategory(id);history.replaceState(null,'','#'+id);if(instant||reduce.matches){scrollTo({top:end,behavior:'instant'});return;}const begin=performance.now(),duration=Math.min(650,Math.max(300,Math.abs(distance)*.13));function step(now){const t=Math.min(1,(now-begin)/duration),ease=1-Math.pow(1-t,3);scrollTo({top:start+distance*ease,behavior:'instant'});if(t<1)scrollAnimation=requestAnimationFrame(step);else{scrollAnimation=0;scrollTo({top:Math.max(0,scrollY+target.getBoundingClientRect().top-offset()),behavior:'instant'});activateCategory(id);}}scrollAnimation=requestAnimationFrame(step);}
if(sections.length){categoryLinks.forEach(a=>a.addEventListener('click',e=>{if(currentTab!=='menu'||e.ctrlKey||e.metaKey||e.shiftKey)return;e.preventDefault();jumpCategory(a.dataset.category);}));window.addEventListener('scroll',()=>{if(!scrollScheduled){scrollScheduled=true;requestAnimationFrame(updateActive);}},{passive:true});for(const type of['wheel','touchstart'])window.addEventListener(type,stopScroll,{passive:true});window.addEventListener('keydown',e=>{if(['PageDown','PageUp','Home','End','ArrowDown','ArrowUp',' '].includes(e.key))stopScroll();});document.addEventListener('click',e=>{const a=e.target.closest('[data-product-link]');if(a)sessionStorage.setItem('mh.menu-position',JSON.stringify({url:location.pathname+location.search+location.hash,y:scrollY}));});document.fonts.ready.then(()=>requestAnimationFrame(()=>{const saved=JSON.parse(sessionStorage.getItem('mh.menu-position')||'null');const restore=sessionStorage.getItem('mh.restore-menu')==='1'||performance.getEntriesByType('navigation')[0]?.type==='back_forward';if(restore&&saved){sessionStorage.removeItem('mh.restore-menu');history.replaceState(null,'',saved.url);scrollTo(0,saved.y);}else if(location.hash)jumpCategory(decodeURIComponent(location.hash.slice(1)),true);updateActive();}));}
document.querySelectorAll('[data-return-menu]').forEach(a=>{const saved=JSON.parse(sessionStorage.getItem('mh.menu-position')||'null');if(saved){a.href=saved.url;a.addEventListener('click',()=>sessionStorage.setItem('mh.restore-menu','1'));}});

function renderSpecial(){if(!currentTab||currentTab==='menu')return;document.querySelector('#catalog-sections').hidden=true;const host=document.querySelector('#special-products');host.hidden=false;document.querySelector('#special-title').textContent=currentTab==='favorites'?'Избранное':'Рекомендуемое';const items=app.catalog.products.filter(p=>currentTab==='favorites'?app.state.favorites.includes(p.id):p.recommended);document.querySelector('#special-grid').innerHTML=items.map(card).join('');document.querySelector('#favorites-empty').hidden=items.length>0||currentTab!=='favorites';updateState();}
document.addEventListener('mh:favorites',renderSpecial);
let detail=null;
function renderDetail(){const host=document.querySelector('[data-product-detail]');if(!host)return;const p=product(host.dataset.productDetail);if(!p)return;detail={p,quantity:1,selections:defaults(p)};configure(document.querySelector('#product-options'),p,detail.selections,updateDetail);}
function updateDetail(){const config=calculated(detail.p,detail.selections);document.querySelector('#detail-quantity').textContent=detail.quantity;document.querySelector('#detail-price').textContent=money(config.price*detail.quantity);document.querySelector('#detail-button-price').textContent=money(config.price*detail.quantity);document.querySelector('#product-allergens').textContent=config.allergens.length?'По выбранной рецептуре: '+config.allergens.join(', '):'В составе отмеченных аллергенов нет. Это не гарантия отсутствия следов.';document.querySelector('#recipe-changes').textContent='Выбранная конфигурация: '+config.summary.join(', ');}
handle('[data-detail-qty]',async b=>{await ready;detail.quantity=Math.max(1,Math.min(99,detail.quantity+Number(b.dataset.detailQty)));updateDetail();});
handle('#detail-add',async()=>{await ready;const cart=await request('/api/cart',{productId:detail.p.id,quantity:detail.quantity,selections:detail.selections},'POST');setCart(cart);toast(`${detail.p.name} — в корзине`);});
let installPrompt;window.addEventListener('beforeinstallprompt',e=>{e.preventDefault();installPrompt=e;});
handle('#install-app',async()=>{if(installPrompt){await installPrompt.prompt();const result=await installPrompt.userChoice;document.querySelector('#install-status').textContent=result.outcome==='accepted'?'Приложение добавлено.':'Можно установить приложение позже.';installPrompt=null;}else{document.querySelector('#install-instructions').scrollIntoView({behavior:reduce.matches?'instant':'smooth'});document.querySelector('#install-status').textContent='Следуйте инструкции ниже для вашего браузера.';}});
if('serviceWorker'in navigator)navigator.serviceWorker.register('/sw.js').catch(()=>{});
ready.then(async()=>{renderSpecial();renderDetail();if(new URLSearchParams(location.search).get('cart')==='1'){document.querySelector('[data-open-cart]').click();history.replaceState(null,'',location.pathname+location.hash);}if(document.querySelector('#reset-password-form'))await(await import('./experience.js?v=20261021-v9')).initReset();if(document.querySelector('#checkout-form'))await(await import('./checkout.js?v=20261021-v9')).initCheckout();if(document.querySelector('#payment-page')||document.querySelector('#order-page')||document.querySelector('#account-content'))await(await import('./orders.js?v=20261021-v9')).initOrders();}).catch(e=>toast(e.message));

ready.then(()=>{if(location.pathname!=='/Menu'||!['recommended','favorites'].includes(new URLSearchParams(location.search).get('tab'))||new URLSearchParams(location.search).has('cart'))return;let interrupted=false;const align=()=>{const target=document.querySelector('#special-products');if(!target||interrupted)return;const header=document.querySelector('.site-header')?.getBoundingClientRect().height??80;const toolbar=document.querySelector('.catalog-toolbar')?.getBoundingClientRect().height??60;scrollTo({top:Math.max(0,scrollY+target.getBoundingClientRect().top-header-toolbar-20),behavior:'instant'});};for(const event of['wheel','touchstart'])window.addEventListener(event,()=>{interrupted=true;},{once:true,passive:true});requestAnimationFrame(align);if(document.readyState!=='complete')window.addEventListener('load',align,{once:true});});
