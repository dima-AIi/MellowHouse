import {it,expect,vi} from 'vitest';
import {act,render,screen,waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {Assistant} from '../src/assistant';
import type {AssistantAnswer,AssistantRequest,Catalog,LegacyRuntime,Product} from '../src/types';

const coffee:Product={id:'coffee',categoryId:'coffee',name:'Капучино',price:300,portion:'300 мл',description:'Кофе с молоком',ingredients:'Молоко',image:'/images/cappuccino.webp',available:true,popular:true,recommended:false,allergens:['молоко'],groups:[]};
const pizza:Product={...coffee,id:'pizza',categoryId:'pizza',name:'Пицца Маргарита',price:520};
const catalog:Catalog={products:[coffee,pizza],categories:[]};
function runtime(){return {
  app:{state:{authenticated:false,profile:null,csrfToken:'test-token',favorites:[],cart:{items:[],count:0,subtotal:0},settings:{faq:[],marketingTitle:'',menuTitle:'',menuText:''}},catalog,method:'pickup'},
  ready:Promise.resolve(),catalogReady:Promise.resolve(catalog),request:vi.fn(),cartView:vi.fn(()=>({items:[],count:0,subtotal:0})),setCart:vi.fn(),updateState:vi.fn(),defaults:vi.fn(()=>({})),calculated:vi.fn((p:Product)=>({price:p.price,allergens:p.allergens,summary:[]})),toast:vi.fn()
} satisfies LegacyRuntime;}
const selection:AssistantAnswer={text:'Вот кофе и пицца.',productIds:['coffee','pizza'],suggestions:['Другие блюда','Другой напиток']};
async function send(text:string){const user=userEvent.setup();await user.type(screen.getByRole('textbox',{name:'Ваш вопрос'}),text);await user.click(screen.getByRole('button',{name:'Отправить вопрос'}));}

it('renders both requested categories using the actual response and sends context without a cart mutation',async()=>{
  const api=runtime();api.request.mockResolvedValue(selection);render(<Assistant runtime={api} catalog={catalog}/>);
  await send('Хочу кофе и пиццу');await screen.findByText('Вот кофе и пицца.');
  expect(screen.getByText('Капучино')).not.toBeNull();expect(screen.getByText('Пицца Маргарита')).not.toBeNull();
  expect(api.request).toHaveBeenCalledOnce();expect(api.request.mock.calls[0]).toMatchObject(['/api/assistant',{message:'Хочу кофе и пиццу',lastProductIds:[],recentQuestions:[]},'POST']);expect(api.setCart).not.toHaveBeenCalled();
});
it('adding the second offered product sends its exact id and applies only the server cart',async()=>{
  const api=runtime(),cart={items:[],count:1,subtotal:520};api.request.mockResolvedValueOnce(selection).mockResolvedValueOnce({...selection,text:'Добавлена пицца.',addedProductIds:['pizza'],cart});
  render(<Assistant runtime={api} catalog={catalog}/>);await send('Кофе и пицца');await screen.findByText('Вот кофе и пицца.');
  await userEvent.click(screen.getAllByRole('button',{name:/^Добавить$/})[1]!);await screen.findByText('Добавлена пицца.');
  expect(api.request.mock.calls[1]?.[1]).toMatchObject({lastProductIds:['pizza'],message:'Добавь этот товар в корзину'});expect(api.setCart).toHaveBeenCalledExactlyOnceWith(cart);
});
it('a network retry reuses the same request id and does not duplicate the customer message',async()=>{
  const api=runtime();api.request.mockRejectedValueOnce(Object.assign(new Error('Нет связи'),{status:503})).mockResolvedValueOnce(selection);
  render(<Assistant runtime={api} catalog={catalog}/>);await send('Кофе и пицца');await screen.findByText('Нет связи');
  await userEvent.click(screen.getByRole('button',{name:'Повторить сообщение'}));await screen.findByText('Вот кофе и пицца.');
  const first=api.request.mock.calls[0]?.[1] as AssistantRequest,second=api.request.mock.calls[1]?.[1] as AssistantRequest;
  expect(second.requestId).toBe(first.requestId);expect(screen.getAllByText('Кофе и пицца')).toHaveLength(1);
});
it('navigation restores the prior dialogue and draft; a private-history reset clears them',async()=>{
  const api=runtime();api.request.mockResolvedValue(selection);const first=render(<Assistant runtime={api} catalog={catalog}/>);
  await send('Кофе и пицца');await screen.findByText('Вот кофе и пицца.');await userEvent.type(screen.getByRole('textbox',{name:'Ваш вопрос'}),'А ещё десерт');first.unmount();
  render(<Assistant runtime={api} catalog={catalog}/>);await screen.findByText('Вот кофе и пицца.');
  expect((screen.getByRole('textbox',{name:'Ваш вопрос'}) as HTMLInputElement).value).toBe('А ещё десерт');
  act(()=>document.dispatchEvent(new CustomEvent('mh:chat-reset')));await waitFor(()=>expect(screen.queryByText('Вот кофе и пицца.')).toBeNull());
  expect((screen.getByRole('textbox',{name:'Ваш вопрос'}) as HTMLInputElement).value).toBe('');
});
it('configuring an offered product preserves the existing server add contract and never checks out',async()=>{
  const api=runtime();api.request.mockResolvedValueOnce(selection).mockResolvedValueOnce({items:[],count:1,subtotal:300});
  render(<Assistant runtime={api} catalog={catalog}/>);await send('Кофе');await screen.findByText('Вот кофе и пицца.');
  await userEvent.click(screen.getAllByRole('button',{name:'Настроить'})[0]!);await screen.findByRole('dialog');
  await userEvent.click(screen.getByRole('button',{name:/Добавить в корзину ·/}));
  await waitFor(()=>expect(api.request).toHaveBeenCalledTimes(2));expect(api.request.mock.calls[1]).toMatchObject(['/api/cart',{productId:'coffee',selections:{},quantity:1},'POST']);
  expect(api.request.mock.calls.some(call=>String(call[0]).includes('checkout'))).toBe(false);
});
it('an in-flight private reply cannot reappear after logout clears the conversation',async()=>{
  const api=runtime();let complete!:(answer:AssistantAnswer)=>void;
  api.request.mockReturnValue(new Promise<AssistantAnswer>(resolve=>{complete=resolve;}));
  render(<Assistant runtime={api} catalog={catalog}/>);await send('Мой последний заказ');
  await waitFor(()=>expect(api.request).toHaveBeenCalledOnce());
  act(()=>document.dispatchEvent(new CustomEvent('mh:chat-reset')));
  await act(async()=>{complete({...selection,text:'Личная история прежнего клиента'});});
  expect(screen.queryByText('Личная история прежнего клиента')).toBeNull();
});
