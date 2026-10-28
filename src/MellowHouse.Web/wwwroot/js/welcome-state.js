// Migrate a legacy installed shortcut without redirecting in-app navigation or order links.
(()=>{const standalone=matchMedia('(display-mode:standalone)').matches||navigator.standalone===true;
 const navigation=performance.getEntriesByType('navigation')[0]?.type;
 if(standalone&&navigation==='navigate'&&!document.referrer&&/^\/Menu\/?$/i.test(location.pathname)&&!location.search&&!location.hash){location.replace('/');}
})();
(()=>{if(location.pathname!=='/')return;const root=document.documentElement;let repeat=true;try{repeat=sessionStorage.getItem('mh.hero.v5')==='1';sessionStorage.setItem('mh.hero.v5','1');}catch{}root.classList.add(repeat?'mh-welcome-return':'mh-welcome-first');setTimeout(()=>root.classList.add('mh-welcome-ready'),5000);})();
