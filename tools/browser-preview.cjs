const {chromium}=require(process.env.MH_PLAYWRIGHT_PATH||'playwright');
const fs=require('node:fs/promises');const path=require('node:path');
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true});
 const context=await browser.newContext({viewport:{width:1440,height:1000},locale:'ru-RU'});
 await context.route('**/*',route=>new URL(route.request().url()).hostname==='localhost'?route.continue():route.abort());
 const page=await context.newPage();let errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
 await page.goto('http://localhost:5186/',{waitUntil:'networkidle'});await page.evaluate(()=>document.fonts.ready);await page.screenshot({path:path.resolve('artifacts/screenshots/01-home-desktop.png'),fullPage:true});
 console.log(JSON.stringify({title:await page.title(),h1:await page.locator('h1').innerText(),errors},null,2));
 await page.goto('http://localhost:5186/Product/vanilla-raf',{waitUntil:'networkidle'});await page.screenshot({path:path.resolve('artifacts/screenshots/03-raf-desktop.png'),fullPage:true});
 console.log(JSON.stringify({options:await page.locator('#product-options').innerText(),errors},null,2));
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
