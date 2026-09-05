// CL-1 探针：纯 JS 聊天页基线（对照组）。
//   node tools/measure-js-baseline.mjs --chat-page <url> --game-page <url> --ws <wsUri> --runs 5 [--channel chrome]
// chat-page = 本仓 modules/web/chat/index.html（纯表现模块，无网络）：量体积与冷启动（DOMContentLoaded、window.__lumioChat 就位）。
// game-page = LumioGame integration/entity-chat/web/index.html（现行宿主验收用的浏览器页，有 room 连接）：量导航 → 连上宿主 → 首帧 的时间。
import { chromium } from 'playwright';

const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : def; };
const runs = Number(opt('--runs', '5'));
const channel = opt('--channel', null);
const stats = (v) => { const s = [...v].sort((a, b) => a - b); return { n: s.length, median: s[Math.floor(s.length / 2)], worst: s[s.length - 1], best: s[0], all: s }; };
const out = (o) => console.log('RESULT ' + JSON.stringify(o));

const browser = await chromium.launch({ headless: true, channel: channel || undefined });
const version = browser.version();

const chatPage = opt('--chat-page');
if (chatPage) {
  const samples = [];
  for (let i = 0; i < runs; i++) {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    await page.goto(chatPage, { waitUntil: 'commit' });
    await page.waitForFunction(() => window.__lumioChat !== undefined);
    const s = await page.evaluate(() => {
      const nav = performance.getEntriesByType('navigation')[0];
      const res = performance.getEntriesByType('resource');
      return { readyMs: performance.now(), domContentLoadedMs: nav.domContentLoadedEventEnd, loadMs: nav.loadEventEnd, transferBytes: nav.transferSize + res.reduce((a, r) => a + r.transferSize, 0), decodedBytes: nav.decodedBodySize + res.reduce((a, r) => a + r.decodedBodySize, 0), resources: res.length + 1 };
    });
    samples.push(s);
    await ctx.close();
  }
  out({ scenario: 'js-chat-page', browser: version, page: chatPage, readyMs: stats(samples.map((s) => s.readyMs)), domContentLoadedMs: stats(samples.map((s) => s.domContentLoadedMs)), transferBytes: samples[0].transferBytes, decodedBytes: samples[0].decodedBytes, resources: samples[0].resources, samples });
}

const gamePage = opt('--game-page');
const ws = opt('--ws');
if (gamePage && ws) {
  const samples = [];
  for (let i = 0; i < runs; i++) {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    const url = `${gamePage}?room=${encodeURIComponent(ws)}&login=Browser01&connectionId=c-jsbaseline-${i}`;
    await page.goto(url, { waitUntil: 'commit' });
    await page.waitForFunction(() => window.__lumioStartLogin !== undefined);
    const tReady = await page.evaluate(() => performance.now());
    await page.evaluate(() => window.__lumioStartLogin('unused'));
    await page.waitForFunction(() => window.__lumioResult && window.__lumioResult.status === 'ok', null, { timeout: 20000 });
    const s = await page.evaluate(() => ({ firstFrameMs: performance.now(), room: window.__lumioResult.room, received: window.__lumioResult.received.length }));
    await page.waitForTimeout(2000);
    const after = await page.evaluate(() => ({ received: window.__lumioResult.received.length, lines: window.__lumioResult.window ? window.__lumioResult.window.lines?.length : null, chatLines: window.__lumioChat.window.lines.length }));
    samples.push({ pageReadyMs: tReady, firstFrameMs: s.firstFrameMs, connectToFirstFrameMs: s.firstFrameMs - tReady, chatLinesAfter2s: after.chatLines });
    await ctx.close();
  }
  out({ scenario: 'js-game-page-connect', browser: version, page: gamePage, ws, pageReadyMs: stats(samples.map((s) => s.pageReadyMs)), firstFrameMs: stats(samples.map((s) => s.firstFrameMs)), connectToFirstFrameMs: stats(samples.map((s) => s.connectToFirstFrameMs)), samples });
}
await browser.close();
