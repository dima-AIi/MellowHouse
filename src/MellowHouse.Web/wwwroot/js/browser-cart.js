export const browserCartKey='mh.browser-cart.v17';
export function configureLocal(product,input){
 if(!product?.available)throw new Error('Эта позиция сейчас недоступна.');
 const selections={},summary=[];let price=product.price;
 for(const group of product.groups){
  const ids=[...new Set(input?.[group.id]??group.defaults)].sort();
  if((group.required&&!ids.length)||(!group.multiple&&ids.length>1)||ids.length<(group.min??0)||ids.length>(group.max??Infinity))throw new Error('Проверьте настройки: '+group.label);
  selections[group.id]=ids;
  for(const id of ids){const option=group.options.find(o=>o.id===id);if(!option)throw new Error('Недоступная настройка: '+group.label);price+=option.price;summary.push(option.label);}
 }
 if(!Number.isSafeInteger(price)||price<0)throw new Error('Не удалось рассчитать цену.');
 return {selections,summary,price,canonical:JSON.stringify(Object.fromEntries(Object.entries(selections).sort(([a],[b])=>a.localeCompare(b))))};
}
export class BrowserCart{
 constructor(storage,{uuid=()=>crypto.randomUUID(),onChange=()=>{}}={}){this.storage=storage;this.uuid=uuid;this.onChange=onChange;}
 read(){
  let raw;try{raw=this.storage.getItem(browserCartKey);}catch{return null;}
  if(!raw)return null;
  try{const value=JSON.parse(raw);if(typeof value.id!=='string'||!Array.isArray(value.lines)||value.lines.length>50)throw new Error();
   if(value.lines.some(l=>typeof l.id!=='string'||typeof l.productId!=='string'||!Number.isInteger(l.quantity)||l.quantity<1||l.quantity>99||!l.selections||typeof l.selections!=='object'))throw new Error();
   return value;
  }catch{return null;}
 }
 save(value){try{this.storage.setItem(browserCartKey,JSON.stringify(value));}catch{throw new Error('Браузер не смог сохранить корзину. Освободите место или разрешите хранение данных сайта.');}this.onChange();}
 editable(){const value=this.read()??{id:this.uuid(),lines:[],frozen:false};if(value.frozen)throw new Error('Сверяем сохранённую корзину с кафе. Подождите подключения или нажмите «Повторить».');return value;}
 add(product,selections,quantity=1){
  if(!Number.isInteger(quantity)||quantity<1||quantity>99)throw new Error('Количество — от 1 до 99.');
  const value=this.editable(),config=configureLocal(product,selections);
  const line=value.lines.find(l=>l.productId===product.id&&l.canonical===config.canonical);
  if(line){if(line.quantity+quantity>99)throw new Error('Не больше 99 одинаковых позиций.');line.quantity+=quantity;}
  else{if(value.lines.length>=50)throw new Error('В корзине может быть до 50 вариантов блюд.');value.lines.push({id:this.uuid(),productId:product.id,name:product.name,selections:config.selections,canonical:config.canonical,expectedUnitPrice:config.price,quantity});}
  this.save(value);
 }
 edit(id,product,selections,quantity){
  if(!Number.isInteger(quantity)||quantity<1||quantity>99)throw new Error('Количество — от 1 до 99.');
  const value=this.editable(),line=value.lines.find(l=>l.id===id);if(!line)throw new Error('Позиция уже изменилась. Откройте корзину заново.');
  const config=configureLocal(product,selections??line.selections),same=value.lines.find(l=>l.id!==id&&l.productId===product.id&&l.canonical===config.canonical);
  if(same){if(same.quantity+quantity>99)throw new Error('Не больше 99 одинаковых позиций.');same.quantity+=quantity;value.lines=value.lines.filter(l=>l.id!==id);}
  else Object.assign(line,{selections:config.selections,canonical:config.canonical,expectedUnitPrice:config.price,quantity});
  this.save(value);
 }
 remove(id){const value=this.editable();value.lines=value.lines.filter(l=>l.id!==id);this.save(value);}
 clear(){const value=this.editable();value.lines=[];this.save(value);}
 freeze(){const value=this.read();if(!value?.lines.length)return null;if(!value.frozen){value.frozen=true;this.save(value);}return value;}
 acknowledge(id){if(this.read()?.id===id){this.storage.removeItem(browserCartKey);this.onChange();}}
 view(products=[]){
  const value=this.read(),items=[];
  for(const line of value?.lines??[]){
   const p=products.find(p=>p.id===line.productId)??{id:line.productId,name:line.name||'Товар',image:'/images/pwa-192.png',portion:'',groups:[],available:false,price:line.expectedUnitPrice};
   let config;try{config=configureLocal(p,line.selections);}catch{config={price:line.expectedUnitPrice??0,summary:['Будет проверено после подключения']};}
   items.push({id:line.id,product:p,selections:line.selections,summary:config.summary,quantity:line.quantity,unitPrice:config.price,total:config.price*line.quantity,available:p.available});
  }
  return {items,subtotal:items.reduce((n,l)=>n+l.total,0),count:items.reduce((n,l)=>n+l.quantity,0),revision:0};
 }
}
