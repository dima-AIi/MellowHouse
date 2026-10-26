import test from 'node:test';
import assert from 'node:assert/strict';
import {BrowserCart,browserCartKey} from '../src/MellowHouse.Web/wwwroot/js/browser-cart.js';
const product={id:'latte',name:'Латте',price:300,available:true,groups:[{id:'milk',label:'Молоко',required:true,multiple:false,defaults:['regular'],options:[{id:'regular',label:'Обычное',price:0},{id:'oat',label:'Овсяное',price:60}]}]};
function setup(){const values=new Map();let n=0;const storage={getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,v),removeItem:k=>values.delete(k)};return{storage,create:()=>new BrowserCart(storage,{uuid:()=>String(++n)})};}
test('adding before a server connection updates quantity, configuration and estimated sum immediately',()=>{
 const f=setup(),cart=f.create();cart.add(product);cart.add(product);cart.add(product,{milk:['oat']},2);
 const view=cart.view([product]);assert.equal(view.count,4);assert.equal(view.subtotal,1320);assert.equal(view.items.length,2);assert.deepEqual(view.items[1].summary,['Овсяное']);
});
test('page navigation and reload restore the same draft and retry identity',()=>{
 const f=setup(),first=f.create();first.add(product);const id=first.read().id;
 const next=f.create();assert.equal(next.read().id,id);assert.equal(next.view([product]).count,1);next.add(product);assert.equal(first.view([product]).count,2);
});
test('decreasing, removing and clearing before connection do not leave queued additions behind',()=>{
 const f=setup(),cart=f.create();cart.add(product,undefined,5);const line=cart.view([product]).items[0];cart.edit(line.id,product,line.selections,2);assert.equal(cart.view([product]).count,2);cart.remove(line.id);assert.equal(cart.freeze(),null);
 cart.add(product);cart.clear();assert.equal(cart.view([product]).count,0);assert.equal(cart.freeze(),null);
});
test('editing a configuration merges matching local variants instead of losing quantities',()=>{
 const f=setup(),cart=f.create();cart.add(product);cart.add(product,{milk:['oat']},2);const row=cart.view([product]).items[1];cart.edit(row.id,product,{milk:['regular']},2);
 assert.equal(cart.view([product]).items.length,1);assert.equal(cart.view([product]).count,3);
});
test('an in-flight draft survives a reload unchanged and cannot be silently edited after an uncertain response',()=>{
 const f=setup(),cart=f.create();cart.add(product,undefined,3);const batch=cart.freeze();const reloaded=f.create();assert.deepEqual(reloaded.freeze(),batch);assert.throws(()=>reloaded.add(product),/Сверяем/);assert.throws(()=>reloaded.clear(),/Сверяем/);
 reloaded.acknowledge('other-batch');assert.equal(cart.view([product]).count,3);reloaded.acknowledge(batch.id);assert.equal(cart.read(),null);
});
test('two tabs observe the shared draft and the freeze before writing',()=>{
 const f=setup(),a=f.create(),b=f.create();a.add(product);b.add(product);assert.equal(a.view([product]).count,2);a.freeze();assert.throws(()=>b.edit(b.view([product]).items[0].id,product,undefined,1),/Сверяем/);
});
test('invalid or excessive quantity and unavailable options are rejected before storage changes',()=>{
 const f=setup(),cart=f.create();for(const q of [0,-1,100,1.5])assert.throws(()=>cart.add(product,undefined,q));cart.add(product,undefined,99);assert.throws(()=>cart.add(product));assert.equal(cart.view([product]).count,99);assert.throws(()=>cart.add(product,{milk:['missing']}));assert.throws(()=>cart.add({...product,available:false}));
});
test('a storage failure is reported instead of falsely promising a saved basket',()=>{
 const cart=new BrowserCart({getItem:()=>null,setItem:()=>{throw Error('quota');},removeItem:()=>{}},{uuid:()=> 'id'});assert.throws(()=>cart.add(product),/не смог сохранить/);
});
test('damaged storage does not break the menu, and a removed item remains visible for reconciliation',()=>{
 const f=setup();f.storage.setItem(browserCartKey,'broken');const cart=f.create();assert.equal(cart.view([product]).count,0);cart.add(product);const view=cart.view([]);assert.equal(view.count,1);assert.equal(view.items[0].available,false);assert.match(view.items[0].summary[0],/проверено/);
});
