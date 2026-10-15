const standaloneFixture = require('./standalone-fixture.cjs');
const { chromium } = require('playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
const fs = require('node:fs');
(async () => {
  const standalone = standaloneFixture(path.resolve(__dirname, '../index.html'));
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 760 }, reducedMotion:'reduce' });
    await standalone.attach(page);
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const check = (value, message) => { if (!value) throw new Error(message); };
  try {
    await page.goto(standalone.url);
    check(await page.locator('.window').evaluate(el=>el.getBoundingClientRect().x===18&&parseFloat(getComputedStyle(el).borderTopWidth)>0),'Desktop window frame');
    check(await page.locator('.captions button').count()===3,'Caption controls');
    check(JSON.stringify(await page.locator('.captions > button').evaluateAll(items=>items.map(item=>item.dataset.window)))===JSON.stringify(['minimize','maximize','close']),'Caption actions belong to the three desktop buttons');
    await page.locator('.captions [data-window="maximize"]').click();check(await page.locator('.window').evaluate(el=>el.classList.contains('maximized')&&el.getBoundingClientRect().x===0),'Maximize changes the frame');
    await page.locator('.captions [data-window="maximize"]').click();check(await page.locator('.window').evaluate(el=>!el.classList.contains('maximized')&&el.getBoundingClientRect().x===18),'Restore resets the frame');
    for(const action of ['minimize','close']){await page.locator('.captions [data-window="'+action+'"]').click();check(!await page.locator('.window').isVisible()&&await page.locator('#restore-window').isVisible(),'Caption hides the window: '+action);await page.locator('#restore-window').click();check(await page.locator('.window').isVisible(),'Restore reopens the window');}
    check(await page.locator('#title-swatches button').count()===8,'Eight title swatches');check(await page.locator('.titlebar select').count()===0,'No duplicate title selectors');
    check(JSON.stringify(await page.locator('#title-swatches button').evaluateAll(items=>items.map(item=>item.dataset.palette)))===JSON.stringify(['DotNet','Aurora','Ember','Default','Aquatic','Desert','Dusk','NightSky']),'Title palette order follows the native enum');
    const nativeSwatches=await page.locator('#title-swatches button').evaluateAll(items=>items.map(item=>getComputedStyle(item,':before').background));check(/linear-gradient.*137, 104, 255.*38, 212, 204/.test(nativeSwatches[1]),'Aurora matches the fixed native gradient swatch');
    await page.locator('#language-button').click();check(await page.locator('#title-language-menu').isVisible(),'Real language menu');await page.locator('#title-language-menu [data-culture="en-US"]').click();check(await page.locator('html').getAttribute('lang')==='en-US','Title language selection');
    for(let index=0;index<3;index++)await page.locator('#theme-cycle').click();check(await page.locator('html').getAttribute('data-mode')==='system','Three-state title cycle');
    await page.goto(standalone.url+'?capture=window');
    for (const route of ['process','files','hardware','settings']) {
      await page.click(`[data-route="${route}"]`);
      check((await page.locator('#page').innerText()).length > 15, 'Empty route: '+route);
    }
    for (const culture of ['en-US','zh-CN','zh-Hant','ja-JP']) {
      await page.click(`input[name="language"][value="${culture}"]`);
      check(await page.locator('html').getAttribute('lang') === culture,'Language mismatch');
      for (const mode of ['light','dark','system']) {
        await page.selectOption('#mode',mode);
        for (const palette of ['DotNet','Aurora','Ember','Default','Aquatic','Desert','Dusk','NightSky']) {
          await page.selectOption('#palette',palette);
          check(await page.locator('html').getAttribute('data-palette') === palette,'Palette mismatch');
          check(JSON.stringify(await page.locator('#title-swatches button').evaluateAll(items=>items.map(item=>getComputedStyle(item,':before').background)))===JSON.stringify(nativeSwatches),'Title swatches stay fixed across palette and mode changes');
          check(await page.locator('#palette').evaluate(el=>getComputedStyle(el).textAlign) === 'left','Select alignment');
        }
      }
      await page.emulateMedia({ colorScheme:'dark' });
      await page.waitForFunction(()=>document.documentElement.dataset.theme==='dark');
      check(await page.locator('html').getAttribute('data-theme') === 'dark','System dark');
      await page.emulateMedia({ colorScheme:'light' });
      await page.waitForFunction(()=>document.documentElement.dataset.theme==='light');
      check(await page.locator('html').getAttribute('data-theme') === 'light','System light');
      await page.click('[data-tab="about"]');
      check((await page.locator('.about dl').innerText()).includes('2026-10-15 00:00:00'),'Sample build time');
      for (const scenario of ['current','available','failed']) {
        await page.evaluate(value=>{window.updateScenario=value;state='idle';update()},scenario);
        await page.click('#action');
        await page.waitForFunction(()=>state!=='checking');
        check(await page.evaluate(()=>state) === scenario,'Update state: '+scenario);
      }
      await page.evaluate(()=>{state='available';update()});
      await page.click('#action');
      await page.click('#cancel');
      check(await page.evaluate(()=>state) === 'canceled','Cancel download');
      await page.click('[data-tab="appearance"]');
    }
    await page.click('[data-route="process"]');await page.click('[data-process-tab="tree"]');
    check(await page.locator('tr[data-pid]').count()===3,'Process tree rows');
    await page.check('[data-filter="FilterHighCpu"]');check(await page.locator('tr[data-pid]').count()===1&&await page.locator('tr[data-pid]').getAttribute('data-pid')==='5120','High CPU filter');await page.uncheck('[data-filter="FilterHighCpu"]');
    await page.check('[data-filter="FilterHasNetworkConnections"]');check(await page.locator('tr[data-pid]').count()===2,'Network process filter');await page.uncheck('[data-filter="FilterHasNetworkConnections"]');
    for(const key of ['FilterUserProcesses','FilterHideSystemProcesses']){await page.check('[data-filter="'+key+'"]');check(await page.locator('tr[data-pid]').count()===2,'User/system process filter');await page.uncheck('[data-filter="'+key+'"]');}
    await page.fill('#search','dotnet');check(await page.locator('tr[data-pid]').count()===1,'Title query filters tree');await page.fill('#search','');
    await page.click('[data-pid="5120"]');await page.click('[data-detail="Details"]');check((await page.locator('#detail-content').innerText()).includes('dotnet'),'Process details');
    await page.click('#end-process');check(await page.locator('#confirm-dialog').evaluate(el=>el.open),'Termination confirmation');await page.click('#confirm-cancel');check(await page.locator('tr[data-pid]').count()===3,'Canceled termination preserves rows');
    await page.click('[data-process-tab="treemap"]');check(await page.locator('.treemap button').count()===3,'Treemap');
    await page.click('[data-route="files"]');await page.fill('#file-query','md');await page.click('#find');await page.waitForSelector('.search-results tbody tr');check(await page.locator('.search-results tbody tr').count()===2,'File search results');
    await page.click('[data-route="hardware"]');await page.click('#refresh-hardware');check(await page.locator('.hardware-sections section').count()===3,'Hardware sections');
    await page.reload();check(await page.locator('html').getAttribute('lang')==='ja-JP','Language persistence');check(await page.locator('html').getAttribute('data-palette')==='NightSky','Palette persistence');
    const output=path.resolve(process.env.NEXUS_AUDIT_OUTPUT||path.join(__dirname,'../../artifacts/prototype-audit'));fs.mkdirSync(output,{recursive:true});
    async function capture(name,selector){await page.screenshot({path:path.join(output,name+'.png')});const geometry=await page.evaluate(selectors=>Object.fromEntries(Object.entries(selectors).map(([key,selector])=>{const rect=document.querySelector(selector).getBoundingClientRect();return[key,{x:rect.x,y:rect.y,width:rect.width,height:rect.height}];})),selector);fs.writeFileSync(path.join(output,name+'.geometry.json'),JSON.stringify(geometry,null,2));}
    await page.setViewportSize({width:1440,height:760});
    await page.evaluate(()=>NexusPrototype.set({culture:'zh-CN',mode:'light',palette:'DotNet',route:'process'}));
    await capture('workspace', {titlebar:'.titlebar',navigation:'#nav',workspace:'.content',cards:'.cards',card:'.card',log:'.log',status:'footer'});
    await page.setViewportSize({width:1080,height:680});await page.goto(standalone.url+'?capture=settings');
    for(const culture of ['en-US','zh-CN','zh-Hant','ja-JP']){
      await page.evaluate(value=>NexusPrototype.set({culture:value,mode:'dark',palette:'DotNet',route:'settings',tab:'appearance'}),culture);
      await capture('appearance-'+culture,{surface:'.settings',navigation:'.settings-nav',form:'.form',mode:'#mode',palette:'#palette'});
      await page.click('[data-tab="about"]');await page.evaluate(()=>{state='idle';update();});await capture('about-'+culture,{surface:'.settings',navigation:'.settings-nav',head:'.about-head',metadata:'.about dl',update:'.update'});
    }
    check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Horizontal overflow');
    check(errors.length===0,errors.join('\n'));
    console.log('PASS desktop window chrome, four routes/languages, 96 appearance combinations, persisted preferences, process tree/details/treemap, file search, hardware, update/cancel and same-size settings captures.');
  await standalone.verify(page);
  } finally { await browser.close(); standalone.dispose(); }
})().catch(error=>{ console.error(error); process.exitCode=1; });
