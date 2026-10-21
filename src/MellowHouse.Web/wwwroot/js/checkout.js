import{validPhone,bindPhones}from'./forms.js?v=20261021-v8';
import{app,request,setCart,updateState,money,esc,totals,icon,busy,toast}from'./core.js?v=20261021-v8';
import{openAuth}from'./cart-auth.js?v=20261021-v8';
export async function initCheckout(){
 const form=document.querySelector('#checkout-form');
 if(!app.state.authenticated){form.hidden=true;document.querySelector('#checkout-auth-notice').hidden=false;openAuth(()=>location.reload());return;}
 if(!app.state.cart.items.length){form.innerHTML=`<div class="empty-state">${icon('bag')}<h2>В корзине пока пусто</h2><p>Сначала выберите любимые блюда.</p><a href="/Menu" class="btn">Открыть меню</a></div>`;return;}
 const labels={card:'Банковская карта',sbp:'СБП',yoomoney:'ЮMoney',cash:'Наличными при получении',terminal:'Картой при получении'};document.querySelector('#checkout-payment-choices').innerHTML=app.state.settings.paymentMethods.map((m,i)=>`<label class="choice"><input type="radio" name="paymentMethod" value="${m}" ${i===0?'checked':''}/>${labels[m]}</label>`).join('');
 let draft={};try{draft=await request('/api/checkout-draft');}catch(e){toast(e.message);}
 let requestKey=draft.requestKey??crypto.randomUUID(),signature=draft.signature??null;
 const controls=name=>form.elements.namedItem(name);
 for(const [name,value]of Object.entries(draft)){const control=controls(name);if(!control||['requestKey','signature'].includes(name))continue;if(name==='consent')control.checked=!!value;else if(typeof value==='string')control.value=value;}
 const chosenMethod=localStorage.getItem('mh.method');if(chosenMethod==='delivery'||chosenMethod==='pickup')app.method=chosenMethod;else if(draft.method==='delivery'||draft.method==='pickup')app.method=draft.method;
 if(!controls('name').value&&app.state.profile?.name)controls('name').value=app.state.profile.name;
 if(!controls('phone').value&&/^7\d{10}$/.test(app.state.profile?.deliveryPhone||app.state.profile?.contact||''))controls('phone').value='+'+(app.state.profile.deliveryPhone||app.state.profile.contact);
 bindPhones(form);updateState();
 function values(){const fd=new FormData(form);return{requestKey,method:app.method,city:fd.get('city')??'',street:fd.get('street')??'',apartment:fd.get('apartment')??'',name:fd.get('name')??'',phone:fd.get('phone')??'',cafe:fd.get('cafe')??'',timing:fd.get('timing')??'asap',scheduledLocal:fd.get('scheduledLocal')??'',comment:fd.get('comment')??'',paymentMethod:fd.get('paymentMethod')??'card',consent:fd.get('consent')==='on'};}
 function render(){
  const delivery=app.method==='delivery';document.querySelector('#comment').placeholder=delivery?'Домофон, ориентир, как удобнее передать заказ…':'Пожелания к приготовлению и упаковке…';document.querySelector('#cafe option[value="demo-house"]').textContent=app.state.settings.cafeName;document.querySelector('#pickup-details').textContent=[app.state.settings.address,app.state.settings.hours,'Самовывоз — бесплатно.'].filter(Boolean).join(' · ');document.querySelector('#delivery-fields').hidden=!delivery;document.querySelector('#pickup-fields').hidden=delivery;document.querySelector('#scheduled-field').hidden=controls('timing').value!=='scheduled';
  document.querySelector('#checkout-items').innerHTML=app.state.cart.items.map(i=>`<div class="summary-line"><img src="${esc(i.product.image)}" alt="${esc(i.product.name)}" width="58" height="60"/><div><h3>${esc(i.product.name)}</h3><p>${esc(i.summary.join(', ')||i.product.portion)}</p><div class="quantity"><button type="button" data-line-qty="${i.id}" data-delta="-1" aria-label="Уменьшить количество ${esc(i.product.name)}">−</button><output>${i.quantity}</output><button type="button" data-line-qty="${i.id}" data-delta="1" aria-label="Увеличить количество ${esc(i.product.name)}">+</button></div></div><strong>${money(i.total)}</strong></div>`).join('');
  const fee=delivery?app.state.deliveryPrice:0;document.querySelector('#checkout-totals').innerHTML=totals(app.state.cart.subtotal,fee);document.querySelector('#place-order').textContent=(['cash','terminal'].includes(controls('paymentMethod').value)?'Оформить заказ · ':'Перейти к оплате · ')+money(app.state.cart.subtotal+fee);
 }
 function clearErrors(){form.querySelectorAll('.form-error').forEach(el=>el.textContent='');form.querySelectorAll('[aria-invalid]').forEach(el=>el.removeAttribute('aria-invalid'));}
 function showError(e){const host=e.field?form.querySelector(`[data-error="${CSS.escape(e.field)}"]`):null;if(host){host.textContent=e.message;const field=controls(e.field);if(field&&typeof field.focus==='function'){field.setAttribute('aria-invalid','true');field.focus();}}else document.querySelector('#checkout-error').textContent=e.message;}
 let saveTimer;async function saveDraft(){try{await request('/api/checkout-draft',{...values(),signature},'PUT');document.querySelector('#draft-status').textContent='Данные оформления сохранены';}catch(e){document.querySelector('#draft-status').textContent=e.message;}}
 function clearField(name){if(!name)return;const error=form.querySelector(`[data-error="${CSS.escape(name)}"]`);if(error)error.textContent='';const field=controls(name);if(field?.removeAttribute)field.removeAttribute('aria-invalid');}
 const changed=e=>{clearField(e?.target?.name);clearTimeout(saveTimer);document.querySelector('#draft-status').textContent='Сохраняем…';saveTimer=setTimeout(saveDraft,550);render();};
 form.addEventListener('change',changed);form.addEventListener('input',e=>{clearField(e.target.name);clearTimeout(saveTimer);saveTimer=setTimeout(saveDraft,700);});
 document.addEventListener('mh:method',()=>{clearErrors();changed();});document.addEventListener('mh:cart',()=>{render();if(!app.state.cart.items.length)location.href='/Menu?cart=1';});
 form.addEventListener('submit',async e=>{e.preventDefault();clearErrors();const button=document.querySelector('#place-order');await busy(button,async()=>{try{
   if(!validPhone(document.querySelector('#phone'))){const error=new Error('Введите телефон: +7 и десять цифр.');error.field='phone';throw error;}
   let body=values();const newSignature=JSON.stringify({...body,requestKey:null,revision:app.state.cart.revision});if(signature&&signature!==newSignature)requestKey=crypto.randomUUID();signature=newSignature;body.requestKey=requestKey;clearTimeout(saveTimer);
   await request('/api/checkout-draft',{...body,signature},'PUT');
   const order=await request('/api/orders',body,'POST');location.href=order.status==='Paid'||['cash','terminal'].includes(order.paymentMethod)?'/Order/'+order.id:'/Payment/'+order.id;
  }catch(error){if(error.status===401)openAuth(()=>location.reload());else showError(error);}});});render();
 const loader=document.querySelector('#checkout-loader');loader.disabled=false;loader.setAttribute('aria-busy','false');
}
