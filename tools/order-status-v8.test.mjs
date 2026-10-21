import test from 'node:test';
import assert from 'node:assert/strict';
import {orderStatus} from '../src/MellowHouse.Web/wwwroot/js/order-status.js';
const order=(fulfillment='New',status='Paid',method='delivery',paymentMethod='sbp')=>({fulfillment,status,paymentMethod,checkout:{method}});
test('payment screen never says an unpaid order is being cooked',()=>{for(const status of ['Pending','Failed','Cancelled']){const s=orderStatus(order('New',status));assert.equal(s.steps[0].code,'Payment');assert.equal(s.steps[1].state,'next');assert.match(s.note,/после подтверждения оплаты/);}});
test('delivery has its own courier and delivered stages',()=>{const s=orderStatus(order('OutForDelivery'));assert.equal(s.title,'У курьера');assert.equal(s.steps.find(x=>x.state==='current').code,'OutForDelivery');assert.equal(s.steps.at(-1).label,'Доставлен');assert.equal(s.steps.filter(x=>x.state==='current').length,1);});
test('pickup omits courier and payment-at-receipt still submits an order',()=>{const s=orderStatus(order('Ready','Pending','pickup','cash'));assert.equal(s.title,'Готов к выдаче');assert.ok(s.steps.every(x=>x.code!=='OutForDelivery'&&x.code!=='Payment'));});
test('new, accepted, completed and rejected have distinct truthful labels',()=>{assert.equal(orderStatus(order()).title,'Заказ получен');assert.equal(orderStatus(order('Accepted')).title,'Принят');assert.equal(orderStatus(order('Completed')).title,'Доставлен');assert.deepEqual(orderStatus(order('Rejected')).steps,[]);});
