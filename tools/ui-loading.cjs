const {chromium}=require(process.env.MH_PLAYWRIGHT_PATH||'playwright');
const fs=require('node:fs/promises');const assert=require('node:assert/strict');
let browser;const results=[],errors=[];const check=(name,value)=>{assert.ok(value,name);results.push({name,passed:true});console.log('PASS '+name);};
(async()=>{
 browser=await chromium.launch({channel:'chrome',headless:true});const context=await browser.newContext({locale:'ru-RU'});
 await context.route('**/*',r=>new URL(r.request().url()).hostname==='localhost'?r.continue():r.abort());
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 let release;let gate=new Promise(resolve=>release=resolve);const slow=async route=>{await gate;await route.continue();};
 await page.route('**/api/catalog',slow);await page.goto('http://localhost:5186/Product/vanilla-raf',{waitUntil:'domcontentloaded'});
 await page.locator('#detail-add').click();check('Добавление ждёт загрузки настроек',await page.locator('#detail-add').isDisabled());release();
 await page.waitForFunction(()=>document.querySelector('.header-cart [data-cart-total]').textContent.includes('320'));
 check('После загрузки добавлена корректная конфигурация',(await page.evaluate(async()=>await(await fetch('/api/state')).json())).cart.subtotal===320);await page.unroute('**/api/catalog',slow);
 await page.locator('.header-cart').click();await page.locator('#checkout-button').click();await page.locator('[data-auth-channel=email]').click();await page.locator('#auth-contact').fill('loading-'+Date.now()+'@example.test');await page.locator('#contact-form [type=submit]').click();await page.locator('#code-form').waitFor();await page.locator('#auth-code').fill('123456');await page.locator('#code-form [type=submit]').click();await page.waitForURL('**/Checkout');await page.locator('#checkout-loader:not([disabled])').waitFor();
 gate=new Promise(resolve=>release=resolve);await page.route('**/api/catalog',slow);await page.reload({waitUntil:'domcontentloaded'});
 check('Оформление заблокировано до загрузки данных',await page.locator('#place-order').isDisabled()&&await page.locator('#customer-name').isDisabled());
 release();await page.locator('#checkout-loader:not([disabled])').waitFor();await page.unroute('**/api/catalog',slow);
 check('Оформление доступно после загрузки, ошибок JS нет',await page.locator('#place-order').isEnabled()&&errors.length===0);
 await fs.writeFile('artifacts/UI-LOADING-RESULTS.json',JSON.stringify({results,errors,date:new Date().toISOString()},null,2));await browser.close();
})().catch(async e=>{console.error(e.stack);await fs.writeFile('artifacts/UI-LOADING-RESULTS.json',JSON.stringify({results,errors,error:e.stack},null,2));if(browser)await browser.close();process.exit(1);});
