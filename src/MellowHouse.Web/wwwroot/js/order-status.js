export function fulfillmentLabel(stage,method){return {New:'Заказ получен',Accepted:'Принят',Preparing:'Готовится',Ready:method==='delivery'?'Готов к отправке':'Готов к выдаче',OutForDelivery:'У курьера',Completed:method==='delivery'?'Доставлен':'Выдан',Rejected:'Отклонён',Cancelled:'Отменён клиентом'}[stage]??stage;}
// Fulfillment comes from the saved order. Time never advances an order's stage.
export function orderStatus(order){
 const delivery=order.checkout.method==='delivery';
 const receipt=['cash','terminal'].includes(order.paymentMethod);
 const paid=['Paid','PartiallyRefunded','Refunded'].includes(order.status);
 if(order.fulfillment==='Cancelled')return{title:'Заказ отменён',note:order.refunded?'Отмена подтверждена. Возврат тестовой оплаты учтён; подробности — в истории заказа.':'Кафе не будет готовить этот заказ. Оплачивать его не нужно.',steps:[]};
 if(order.fulfillment==='Rejected')return{title:'Заказ отклонён',note:'Причина указана в истории ниже. Свяжитесь с кафе, если нужна помощь.',steps:[]};
 if(order.status==='Refunded')return{title:'Оплата возвращена',note:'Полный возврат оплаты отмечен. Подробности указаны в истории заказа.',steps:[]};
 if(!paid&&!receipt){
  const failed=['Failed','Cancelled'].includes(order.status);
  return{title:failed?'Оплата не завершена':'Ожидает оплаты',note:'Заказ сохранён. Кафе начнёт его готовить после подтверждения оплаты.',steps:[{code:'Payment',label:'Оплата',state:'current'},{code:'Preparing',label:'Приготовление',state:'next'},{code:'Completed',label:delivery?'Доставка':'Получение',state:'next'}]};
 }
 const stages=[['New','Получен'],['Accepted','Принят'],['Preparing','Готовится'],['Ready',delivery?'Готов к отправке':'Готов к выдаче'],...(delivery?[['OutForDelivery','У курьера']]:[]),['Completed',delivery?'Доставлен':'Выдан']];
 const index=Math.max(0,stages.findIndex(([code])=>code===order.fulfillment));
 const notes={New:'Заказ передан в кафе и ожидает подтверждения.',Accepted:'Кафе приняло заказ. Скоро начнём готовить.',Preparing:'На кухне готовят ваши блюда и напитки.',Ready:delivery?'Заказ готов. Ожидаем передачи курьеру.':'Заказ готов. Можно забрать его в кафе.',OutForDelivery:'Курьер забрал заказ и направляется по указанному адресу.',Completed:delivery?'Заказ доставлен. Приятного аппетита!':'Заказ выдан. Приятного аппетита!'};
 return{title:order.fulfillment==='New'?'Заказ получен':stages[index][1],note:notes[order.fulfillment]??notes.New,steps:stages.map(([code,label],i)=>({code,label,state:i<index?'complete':i===index?'current':'next'}))};
}
