const rubles=value=>new Intl.NumberFormat('ru-RU',{style:'currency',currency:'RUB',maximumFractionDigits:0}).format(Math.abs(value));
export function optionLabel(group,option){
 if(['sauce','dressing'].includes(group.id)){
  if(option.id==='standard')return 'Вместе с блюдом';
  if(option.id==='apart')return 'В отдельной ёмкости';
 }
 return option.label;
}
export function optionPrice(product,group,option){
 const change=option.price>0?'+'+rubles(option.price):option.price<0?'−'+rubles(option.price):'Без доплаты';
 return group.id==='size'?rubles(product.price+option.price)+' за порцию · '+change:change;
}
export function optionHelp(group){
 return {sauce:'Выберите, как подать соус к блюду.',dressing:'Выберите, как подать заправку.',exclude:'Отметьте ингредиенты, которые не добавлять в блюдо.',milk:'Выберите молоко для напитка.',sweet:'Выберите сладость напитка.',extras:'Отметьте добавки к вашей порции.',size:'Выберите размер порции. Изменение цены указано относительно базовой.'}[group.id]??'';
}
