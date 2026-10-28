import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {runInNewContext} from 'node:vm';
const script=readFileSync(new URL('../src/MellowHouse.Web/wwwroot/js/welcome-state.js',import.meta.url),'utf8');
function run(values={}){let target=null;runInNewContext(script,{location:{pathname:'/Menu',search:'',hash:'',replace:value=>target=value,...values.location},matchMedia:()=>({matches:values.standalone??true}),navigator:{standalone:values.ios??false},performance:{getEntriesByType:()=>[{type:values.navigation??'navigate'}]},document:{referrer:values.referrer??'',documentElement:{classList:{add(){}}}},sessionStorage:{getItem(){return null;},setItem(){}},setTimeout(){}});return target;}
test('new installed applications start on the homepage',()=>{const manifest=JSON.parse(readFileSync(new URL('../src/MellowHouse.Web/wwwroot/manifest.webmanifest',import.meta.url)));assert.equal(manifest.start_url,'/');assert.equal(manifest.id,'/');});
test('legacy Android and iOS menu shortcuts open the homepage',()=>{assert.equal(run(),'/');assert.equal(run({standalone:false,ios:true}),'/');});
test('normal browser visits and internal navigation keep the menu',()=>{assert.equal(run({standalone:false}),null);assert.equal(run({referrer:'https://mellow-house.vercel.app/'}),null);});
test('reload, history navigation, checkout and deep links are never redirected',()=>{for(const navigation of ['reload','back_forward'])assert.equal(run({navigation}),null);for(const location of [{pathname:'/Checkout'},{pathname:'/Order/test'},{hash:'#breakfast'},{search:'?cart=1'},{search:'?tab=favorites'}])assert.equal(run({location}),null);});
