const button=document.querySelector('#mobile-menu-button'),menu=document.querySelector('#mobile-nav');
const mobile=matchMedia('(max-width:700px)'),reduced=matchMedia('(prefers-reduced-motion:reduce)');
let closingTimer;
function closeMenu(){
 if(!menu.open)return;
 menu.classList.remove('is-visible');button.setAttribute('aria-expanded','false');
 clearTimeout(closingTimer);closingTimer=setTimeout(()=>menu.close(),reduced.matches?0:550);
}
button.addEventListener('click',()=>{
 clearTimeout(closingTimer);if(menu.open){closeMenu();return;}
 menu.showModal();button.setAttribute('aria-expanded','true');requestAnimationFrame(()=>requestAnimationFrame(()=>menu.classList.add('is-visible')));
});
menu.querySelector('[data-close-mobile-menu]').addEventListener('click',closeMenu);
menu.addEventListener('cancel',e=>{e.preventDefault();closeMenu();});
menu.addEventListener('click',e=>{const r=menu.getBoundingClientRect();if(e.target===menu&&(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom))closeMenu();if(e.target.closest('a'))closeMenu();});
menu.addEventListener('close',()=>{clearTimeout(closingTimer);menu.classList.remove('is-visible');button.setAttribute('aria-expanded','false');});
menu.querySelectorAll('nav a').forEach(link=>{if(new URL(link.href).pathname===location.pathname)link.setAttribute('aria-current','page');});
function layout(){document.querySelectorAll('.footer-links').forEach(d=>d.open=!mobile.matches);if(!mobile.matches&&menu.open){clearTimeout(closingTimer);menu.close();}}
mobile.addEventListener('change',layout);layout();
// Prevent accidental double-tap zoom. The viewport declaration handles supported mobile browsers.
if(matchMedia('(pointer:coarse)').matches){document.addEventListener('gesturestart',e=>e.preventDefault(),{passive:false});}
