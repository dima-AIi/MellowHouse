import {describe,it,expect,vi} from 'vitest';
import {render,screen,within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {Configurator,MenuSearch,ProductCard,ProductDetail,type View} from '../src/components';
import type {Catalog,Product,OptionGroup,Selections} from '../src/types';

export const product:Product={id:'coffee',categoryId:'coffee',name:'Капучино',price:300,portion:'300 мл',description:'Кофе с молоком',ingredients:'Кофе, молоко',image:'/images/cappuccino.webp',available:true,popular:true,recommended:false,groups:[],allergens:['молоко'],nutrition:'',nutritionFacts:{calories:152,protein:8,fat:8,carbs:12,estimated:true}};
export const pizza:Product={...product,id:'pizza',categoryId:'pizza',name:'Пицца Маргарита',price:520,portion:'420 г',description:'Томаты и сыр',ingredients:'Томат, моцарелла',popular:false};
export const catalog:Catalog={products:[product,pizza],categories:[{id:'coffee',name:'Кофе',image:'',onHome:true,sortOrder:0},{id:'pizza',name:'Пицца',image:'',onHome:true,sortOrder:1}]};
const empty:View={cart:{items:[],count:0,subtotal:0},favorites:[]};
const group:OptionGroup={id:'extras',label:'Добавки',multiple:true,required:false,min:0,max:1,defaults:[],options:[{id:'syrup',label:'Сироп',price:40},{id:'cream',label:'Сливки',price:50}]};

describe('unchanged menu behavior',()=>{
  it('shows the selected product price, availability and real cart count beside the controls',()=>{
    const view:View={cart:{items:[{id:'line',product,quantity:3,selections:{},unitPrice:300,total:900,summary:[]}],count:3,subtotal:900},favorites:['coffee']};
    render(<ProductCard product={product} view={view}/>);
    expect(screen.getByRole('status',{name:'В корзине: Капучино'}).textContent).toBe('3');
    expect(screen.getByRole('button',{name:'Убрать Капучино из избранного'}).getAttribute('aria-pressed')).toBe('true');
    expect(screen.getByRole('button',{name:'Убрать одну порцию: Капучино'}).hidden).toBe(false);
    expect(screen.getByLabelText(/Цена Капучино/).textContent).toMatch(/300/);
  });
  it('does not show nutrition on list cards and disables unavailable products',()=>{
    render(<ProductCard product={{...product,available:false}} view={empty}/>);
    expect(screen.queryByText(/КБЖУ/)).toBeNull();expect((screen.getByRole('button',{name:'Добавить Капучино в корзину'}) as HTMLButtonElement).disabled).toBe(true);
  });
  it('places Russian nutrition inside the product disclosure and escapes catalog text',()=>{
    render(<ProductDetail catalog={catalog} product={{...product,description:'<script>alert(1)</script>'}} view={empty}/>);
    expect(screen.getByText('КБЖУ и пищевая ценность').closest('details')).not.toBeNull();
    expect(document.querySelector('script')).toBeNull();expect(screen.getByText('<script>alert(1)</script>')).not.toBeNull();
    expect(screen.getByText('Белки, г')).not.toBeNull();
  });
  it('searches by several words and ingredients, reports an empty result, and clears the search',async()=>{
    const user=userEvent.setup();render(<MenuSearch catalog={catalog}/>);const input=screen.getByRole('searchbox');
    await user.type(input,'моцарелла');expect(screen.getByRole('status').textContent).toBe('Найдено: 1');
    expect(within(screen.getByRole('region',{name:'Результаты поиска'})).getByRole('link',{name:/Пицца/})).not.toBeNull();
    await user.clear(input);await user.type(input,'неизвестное блюдо');expect(screen.getByRole('status').textContent).toBe('Ничего не найдено');
    await user.click(screen.getByRole('button',{name:'Очистить поиск'}));expect((input as HTMLInputElement).value).toBe('');expect(input).toBe(document.activeElement);
  });
});
describe('React configurator preserves server selections',()=>{
  it('enforces maximum and keeps the previous selection and explanatory price',async()=>{
    const user=userEvent.setup(),change=vi.fn(),invalid=vi.fn();render(<Configurator product={{...product,groups:[group]}} initial={{extras:[]}} onChange={change} onInvalid={invalid}/>);
    await user.click(screen.getByRole('checkbox',{name:/Сироп/}));await user.click(screen.getByRole('checkbox',{name:/Сливки/}));
    expect(change).toHaveBeenLastCalledWith({extras:['syrup']});expect(invalid).toHaveBeenCalledOnce();
    expect((screen.getByRole('checkbox',{name:/Сливки/}) as HTMLInputElement).checked).toBe(false);
    expect(screen.getByText(/\+40/)).not.toBeNull();
  });
  it('enforces minimum and supports a second allowed checkbox',async()=>{
    const user=userEvent.setup(),change=vi.fn(),invalid=vi.fn();render(<Configurator product={{...product,groups:[{...group,required:true,max:2}]}} initial={{extras:['syrup']}} onChange={change} onInvalid={invalid}/>);
    await user.click(screen.getByRole('checkbox',{name:/Сироп/}));expect(invalid).toHaveBeenCalledOnce();
    await user.click(screen.getByRole('checkbox',{name:/Сливки/}));expect(change).toHaveBeenLastCalledWith({extras:['syrup','cream']});
  });
  it('optional radio has a real empty choice and independent configurators do not share names',async()=>{
    const user=userEvent.setup(),change=vi.fn();const optional={...group,multiple:false,max:1};const props={product:{...product,groups:[optional]},initial:{extras:['syrup']} as Selections,onChange:change,onInvalid:vi.fn()};
    render(<><Configurator {...props}/><Configurator {...props}/></>);const choices=screen.getAllByRole('radio',{name:/Без добавления/}) as HTMLInputElement[];
    expect(choices[0]!.name).not.toBe(choices[1]!.name);await user.click(choices[0]!);expect(change).toHaveBeenLastCalledWith({extras:[]});
  });
});
