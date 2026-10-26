import{request}from'./core.js?v=20261026-v17';
const key=id=>'mh.guest-order.'+id;
export function rememberGuestOrder(order){
 if(order.guestAccessToken){try{sessionStorage.setItem(key(order.id),order.guestAccessToken);sessionStorage.setItem('mh.guest-last',order.id);}catch{}}
}
export function orderUrl(id,page='Order'){
 let token='';try{token=sessionStorage.getItem(key(id))??'';}catch{}
 return '/'+page+'/'+id+(token?'#access='+encodeURIComponent(token):'');
}
export async function openGuestOrder(id){
 const token=new URLSearchParams(location.hash.slice(1)).get('access');
 if(!token)return;
 // The secret stays in the fragment and POST body, never in server URLs or referrers.
 try{await request('/api/orders/'+id+'/access',{token},'POST');}
 catch(error){
  if(error.status!==404)throw error;
  // An expired guest link must not lock the original visitor or the new account owner out.
  await request('/api/orders/'+id);
  forgetGuestOrder(id);
  return;
 }
 try{sessionStorage.setItem(key(id),token);sessionStorage.setItem('mh.guest-last',id);}catch{}
}
export function forgetGuestOrder(id){
 try{sessionStorage.removeItem(key(id));if(sessionStorage.getItem('mh.guest-last')===id)sessionStorage.removeItem('mh.guest-last');}catch{}
 if(location.hash.startsWith('#access='))history.replaceState(null,'',location.pathname+location.search);
}
export function lastGuestOrderUrl(){
 try{const id=sessionStorage.getItem('mh.guest-last');return /^[0-9a-f-]{36}$/i.test(id??'')?orderUrl(id):null;}catch{return null;}
}
