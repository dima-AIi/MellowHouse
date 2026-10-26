import test from 'node:test';
import assert from 'node:assert/strict';
import {optionLabel,optionPrice,optionHelp} from '../src/MellowHouse.Web/wwwroot/js/product-options.js';
import {orderStatus,fulfillmentLabel} from '../src/MellowHouse.Web/wwwroot/js/order-status.js';

test('options distinguish included choices, surcharges, reductions and full portion prices',()=>{
 const product={price:320},group={id:'milk'};
 assert.equal(optionPrice(product,group,{price:0}),'Без доплаты');
 assert.match(optionPrice(product,group,{price:60}),/^\+60/);
 assert.match(optionPrice(product,group,{price:-40}),/^−40/);
 const price=optionPrice(product,{id:'size'},{price:-40});assert.match(price,/280.*за порцию.*−40/);
});
test('sauce choices explain serving and exclusion help does not contain technical selection bounds',()=>{
 assert.equal(optionLabel({id:'sauce'},{id:'standard',label:'Стандартно'}),'Вместе с блюдом');
 assert.equal(optionLabel({id:'sauce'},{id:'apart',label:'Отдельно'}),'В отдельной ёмкости');
 assert.equal(optionLabel({id:'sauce'},{id:'bbq',label:'Барбекю'}),'Барбекю');
 assert.match(optionHelp({id:'exclude'}),/не добавлять/);assert.doesNotMatch(optionHelp({id:'exclude'}),/от 0/);
});
test('customer cancellation is distinct from cancelling a payment attempt or café rejection',()=>{
 const order={checkout:{method:'delivery'},paymentMethod:'card',status:'Refunded',fulfillment:'Cancelled',refunded:580};
 assert.equal(fulfillmentLabel('Cancelled','delivery'),'Отменён клиентом');
 assert.equal(orderStatus(order).title,'Заказ отменён');assert.deepEqual(orderStatus(order).steps,[]);
 assert.match(orderStatus(order).note,/Возврат/);
 assert.equal(orderStatus({...order,status:'Cancelled',refunded:0}).title,'Заказ отменён');
 assert.equal(orderStatus({...order,status:'Cancelled',fulfillment:'New',refunded:0}).title,'Оплата не завершена');
});
