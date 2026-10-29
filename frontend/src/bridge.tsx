import {createRoot,type Root} from 'react-dom/client';
import {flushSync} from 'react-dom';
import type {ReactNode} from 'react';
import {Assistant} from './assistant';
import {Header,Footer} from './chrome';
import {CatalogSections,CategoryNav,Configurator,Faq,Icon,MenuSearch,Nutrition,ProductCard,ProductDetail,type View} from './components';
import type {LegacyRuntime,ReactStorefront,PublicSettings} from './types';

/** Small ownership boundaries keep the existing checkout/payment controllers and React from sharing children. */
export function attachStorefront(runtime:LegacyRuntime):ReactStorefront {
  const roots=new WeakMap<Element,Root>();
  function render(host:Element|null,component:ReactNode,name:string){
    if(!host)return;let root=roots.get(host);if(!root){root=createRoot(host);roots.set(host,root);host.setAttribute('data-react-component',name);}
    flushSync(()=>root.render(component));
  }
  function view():View{return {cart:runtime.cartView(),favorites:runtime.app.state?.favorites??[]};}
  const categoriesHost=document.querySelector('.category-nav'),sectionsHost=document.querySelector('#catalog-sections'),popularHost=document.querySelector('#popular-track');
  const photosHost=document.querySelector('.photo-categories'),searchHost=document.querySelector('.catalog-search');
  const detailHost=location.pathname.toLowerCase().startsWith('/product/')?document.querySelector('#main'):null;
  const assistantHost=document.querySelector('.assistant-card'),specialHost=document.querySelector('#special-products');
  const faqHost=document.querySelector('.faq-column .faq-list');
  let allergensHost:HTMLElement|null=null;
  const firstAllergen=document.querySelector('.prose .allergen-item');
  if(firstAllergen){allergensHost=document.createElement('div');firstAllergen.before(allergensHost);firstAllergen.parentElement?.querySelectorAll(':scope > .allergen-item').forEach(node=>node.remove());}
  render(document.querySelector('.header-inner'),<Header view={view()}/>,'Header');
  render(document.querySelector('.footer-grid'),<Footer/>,'Footer');
  function renderCards(){
    const catalog=runtime.app.catalog;if(!catalog)return;
    const current=view();render(sectionsHost,<CatalogSections catalog={catalog} view={current}/>,'Catalog');
    render(popularHost,<>{catalog.products.filter(p=>p.popular&&p.available).map(p=><ProductCard key={p.id} product={p} view={current}/>)}</>,'PopularProducts');
  }
  function renderSpecial(tab?:string){
    const catalog=runtime.app.catalog;if(!catalog||!specialHost)return;
    const active=tab??new URLSearchParams(location.search).get('tab');if(active!=='favorites'&&active!=='recommended')return;
    if(sectionsHost)(sectionsHost as HTMLElement).hidden=true;(specialHost as HTMLElement).hidden=false;
    const current=view(),products=catalog.products.filter(p=>active==='favorites'?current.favorites.includes(p.id):p.recommended);
    render(specialHost,<><h2 id="special-title">{active==='favorites'?'Избранное':'Рекомендуемое'}</h2><div className="product-grid" id="special-grid">{products.map(p=><ProductCard key={p.id} product={p} view={current}/>)}</div><div className="empty-state" id="favorites-empty" hidden={products.length>0||active!=='favorites'||!runtime.app.state}><Icon name="heart"/><h2>Здесь будут ваши любимые блюда</h2><p>Нажмите на сердечко у товара — он останется под рукой.</p><a className="btn" href="/Menu">Открыть меню</a></div></>,'SpecialProducts');
  }
  function renderCatalog(){
    const catalog=runtime.app.catalog;if(!catalog)return;
    render(categoriesHost,<CategoryNav catalog={catalog}/>,'CategoryNav');renderCards();renderSpecial();
    render(photosHost,<>{catalog.categories.filter(c=>c.onHome&&c.image).map(c=><a className="photo-category" href={`/Menu#${c.id}`} key={c.id}><img src={c.image} alt="" width="640" height="426" loading="lazy"/><h3>{c.name}</h3></a>)}</>,'HomeCategories');
    render(searchHost,<MenuSearch catalog={catalog}/>,'MenuSearch');
    if(detailHost){
      let id='';try{id=decodeURIComponent(location.pathname.replace(/\/$/,'').split('/').pop()??'');}catch{/* Unknown encoded URL has the same not-found state. */}
      const product=catalog.products.find(p=>p.id===id);
      render(detailHost,product?<ProductDetail catalog={catalog} product={product} view={view()}/>:<div className="container empty-state"><h1>Товар не найден</h1><p>Возможно, он больше не входит в меню. Выберите что-нибудь вкусное в каталоге.</p><a className="btn" href="/Menu">Открыть меню</a></div>,'ProductDetail');
      document.title=(product?.name??'Товар не найден')+' — Mellow House';
      try{const saved=JSON.parse(sessionStorage.getItem('mh.menu-position')??'null') as {url?:string}|null;const back=document.querySelector<HTMLAnchorElement>('[data-return-menu]');if(saved?.url?.startsWith('/Menu')&&back){back.href=saved.url;back.onclick=()=>{sessionStorage.setItem('mh.restore-menu','1');};}}catch{}
    }
    render(allergensHost,<>{catalog.products.map(p=><article className="allergen-item" key={p.id}><h2><a href={`/Product/${p.id}`}>{p.name}</a></h2><p>{p.ingredients}</p><p><strong>Базовая рецептура:</strong> {p.allergens.length?p.allergens.join(', '):'Отмеченных аллергенов в данных нет; уточните состав в кафе.'}</p><Nutrition product={p}/></article>)}</>,'Allergens');
    render(assistantHost,<Assistant runtime={runtime} catalog={catalog}/>,'Assistant');
  }
  function renderSettings(settings:PublicSettings){render(faqHost,<Faq items={settings.faq}/>,'Faq');}
  document.addEventListener('mh:cart',()=>{renderCards();renderSpecial();});
  return {renderCatalog,renderSpecial,renderSettings,configure(host,product,selections,changed){
    // A previously opened native configurator must not receive a second change listener.
    host.onchange=null;
    render(host,<Configurator key={crypto.randomUUID()} product={product} initial={selections} onInvalid={runtime.toast} onChange={next=>{for(const group of product.groups)selections[group.id]=next[group.id]??[];changed();}}/>,'Configurator');
    host.setAttribute('aria-busy','false');changed();
  }};
}
