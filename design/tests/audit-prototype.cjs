const standaloneFixture = require('./standalone-fixture.cjs');
const { chromium } = require('playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
(async () => {
  const standalone = standaloneFixture(path.resolve(__dirname, '../index.html'));
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 760 } });
    await standalone.attach(page);
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const check = (value, message) => { if (!value) throw new Error(message); };
  try {
    await page.goto(standalone.url);
    for (const route of ['process','files','hardware','settings']) {
      await page.click(`[data-route="${route}"]`);
      check((await page.locator('#page').innerText()).length > 15, 'Empty route: '+route);
    }
    for (const culture of ['en-US','zh-CN','zh-Hant','ja-JP']) {
      await page.selectOption('#language',culture);
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
    check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Horizontal overflow');
    check(errors.length===0,errors.join('\n'));
    console.log('PASS Four routes, four languages, 96 appearance combinations, system changes, update and cancel flows.');
  await standalone.verify(page);
  } finally { await browser.close(); standalone.dispose(); }
})().catch(error=>{ console.error(error); process.exitCode=1; });
