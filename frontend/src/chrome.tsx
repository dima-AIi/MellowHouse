import {Icon,money,type View} from './components';
export function Header({view}: {view:View}) {
  return <><button className="icon-btn hamburger" aria-label="Открыть навигацию" aria-expanded="false" aria-controls="mobile-nav" aria-haspopup="dialog" id="mobile-menu-button"><Icon name="menu"/></button><a className="brand" href="/" translate="no">Mellow House</a>
    <nav className="desktop-nav" aria-label="Основная навигация"><a href="/">Главная</a><a href="/Menu">Меню</a><a href="/Contacts">Контакты</a></nav><div className="header-actions"><a href="/App#install-instructions" className="header-app" aria-label="Установить Mellow House" title="Как установить приложение"><Icon name="app-window"/><span>Приложение</span></a><button className="header-cart" data-open-cart="" aria-label={`Открыть корзину, ${view.cart.count} шт., ${money(view.cart.subtotal)}`}><Icon name="bag"/><span data-cart-total="">{money(view.cart.subtotal)}</span><small data-cart-count="" hidden={!view.cart.count}>{view.cart.count}</small></button><button className="header-account" data-account="" aria-label="Войти"><Icon name="user"/><span data-account-label="">Войти</span></button></div>
  </>;
}
const groups=[
  {name:'Меню',links:[['/Menu#breakfast','Завтраки'],['/Menu#sandwiches','Сэндвичи и закуски'],['/Menu#salads','Салаты и боулы'],['/Menu#hot','Горячие блюда'],['/Menu#pizza','Пицца'],['/Menu#desserts','Десерты и выпечка'],['/Menu#coffee','Кофе и напитки']]},
  {name:'О нас',links:[['/About','Наша история'],['/About#ingredients','Кофе и ингредиенты'],['/Contacts','Контакты'],['/Account','Личный кабинет']]},
  {name:'Доставка и оплата',links:[['/Delivery','Условия доставки'],['/Delivery#payment','Способы оплаты'],['/Delivery#faq','Вопросы и ответы'],['/Legal','Правовая информация'],['/Legal#privacy','Конфиденциальность']]}
];
export function Footer(){return <><div><a className="brand" href="/">Mellow House</a><p>Хорошая еда. Яркие люди.<br/>Каждый день.</p><a href="/Allergens">Состав и аллергены</a></div>
  {groups.map(group=><details key={group.name} className="footer-links" open><summary><span>{group.name}</span><Icon name="chevron-down"/></summary><div>{group.links.map(([href,label])=><a href={href} key={href}>{label}</a>)}</div></details>)}
  <div className="footer-app"><h2>Приложения Mellow House</h2><div className="platform-badges" aria-label="Будущие приложения">{['iPhone','Android','Компьютер'].map(platform=><button type="button" className="platform-badge" data-platform-preview={platform==='Компьютер'?'компьютера':platform} key={platform}><Icon name={platform==='Компьютер'?'app-window':'phone'}/><span>{platform}<small>Скоро</small></span></button>)}</div><p className="platform-note" id="platform-note" role="status"/><a className="footer-install" href="/App#install-instructions"><Icon name="app-window"/><span>Добавить на главный экран</span></a></div>
</>;}
