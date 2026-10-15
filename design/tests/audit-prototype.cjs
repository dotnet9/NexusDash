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
