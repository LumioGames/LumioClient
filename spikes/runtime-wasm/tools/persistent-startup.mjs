// 对照实验：Playwright 持久化 profile（有磁盘缓存与 V8 代码缓存）vs 默认的临时 context。同一页面连续加载 N 次，逐次记 WorldManager 可用时间。
import { chromium } from 'playwright';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
const url = process.argv[2]; const runs = Number(process.argv[3] || '4'); const headless = process.argv.includes('--headless');
const dir = mkdtempSync(join(tmpdir(), 'lumio-pw-profile-'));
const ctx = await chromium.launchPersistentContext(dir, { channel: 'chrome', headless });
const out = [];
for (let i = 0; i < runs; i++) {
  const page = await ctx.newPage();
  await page.goto(url, { waitUntil: 'commit' });
  await page.waitForFunction(() => window.__spike && window.__spike.ready === true, null, { timeout: 120000 });
  const t = await page.evaluate(() => ({ ...window.__spike.timings }));
  out.push({ run: i, worldManagerReadyMs: Math.round(t.bootDone), dotnetCreateMs: Math.round(t.dotnetCreated), transferBytes: t.resources.reduce((s, r) => s + r.transferSize, 0) });
  await page.close();
}
console.log('RESULT ' + JSON.stringify({ scenario: 'startup-persistent-profile', headless, url, browser: ctx.browser()?.version(), runs: out }));
await ctx.close();
