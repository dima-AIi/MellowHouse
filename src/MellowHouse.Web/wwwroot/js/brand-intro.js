// A short welcome once per tab session. Never blocks checkout or a deep link.
const intro=document.querySelector('#brand-intro');
if(intro){let seen=true;try{seen=sessionStorage.getItem('mh.welcome.v4')==='1';sessionStorage.setItem('mh.welcome.v4','1');}catch{seen=false;}
 if(!seen&&!matchMedia('(prefers-reduced-motion: reduce)').matches&&!location.hash){
  intro.hidden=false;const dismiss=()=>{intro.classList.add('leaving');setTimeout(()=>{intro.hidden=true;document.documentElement.classList.add('mh-welcome-ready');},420);};
  setTimeout(dismiss,2500);
  document.addEventListener('keydown',e=>{if(e.key==='Escape')dismiss();},{once:true});
  document.addEventListener('focusin',e=>{if(!intro.contains(e.target))dismiss();});
 }
}
document.querySelectorAll('[data-platform-preview]').forEach(button=>button.addEventListener('click',()=>{const note=document.querySelector('#platform-note');if(note)note.textContent='Версия для '+button.dataset.platformPreview+' готовится. Меню и заказы уже доступны прямо на сайте.';}));

if(!intro||intro.hidden)document.documentElement.classList.add('mh-welcome-ready');
