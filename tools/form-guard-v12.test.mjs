import test from 'node:test';
import assert from 'node:assert/strict';
import {watchForm} from '../src/MellowHouse.Web/wwwroot/js/form-guard.js';
import {image,imageSet} from '../src/MellowHouse.Web/wwwroot/js/product-images.js';
test('optimized product assets have responsive sizes while uploaded and unknown images retain their original path',()=>{
 assert.equal(image('/images/products/syrniki.png',640),'/images/optimized/products/syrniki.png-640.webp');
 assert.match(imageSet('/images/products/syrniki.png'),/768w/);
 for(const path of ['/media/new-product.webp','/images/products/not-generated.png']){assert.equal(image(path),path);assert.equal(imageSet(path),'');}
});
test('initial form is clean; changed text, checkboxes and added options need confirmation',async()=>{
 const text={tagName:'INPUT',type:'text',name:'title',value:'Сырники',checked:false};
 const check={tagName:'INPUT',type:'checkbox',name:'available',value:'on',checked:true};
 const form={elements:[text,check]},changed=watchForm(form);await Promise.resolve();
 assert.equal(changed(),false);text.value='Другой товар';assert.equal(changed(),true);text.value='Сырники';assert.equal(changed(),false);
 check.checked=false;assert.equal(changed(),true);check.checked=true;
 form.elements.push({tagName:'INPUT',type:'text',name:'option',value:'Соус'});assert.equal(changed(),true);
});
