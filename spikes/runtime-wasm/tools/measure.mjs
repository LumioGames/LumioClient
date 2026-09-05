// CL-1 探针：Playwright（Chromium headless）驱动测量。用法：
//   node tools/measure.mjs startup --page <url> --runs 5 [--warm]        冷/热启动（空缓存 = 每次新 context）
//   node tools/measure.mjs rebuild --page <url> --fixtures 100,300,1000 --inputs 5 --repeats 5
//   node tools/measure.mjs wire --page <url> --ws <wsUri> --seconds 300 --chats 5
//   node tools/measure.mjs interop --page <url>
//   node tools/measure.mjs timers --page <url>
//   node tools/measure.mjs draw --page <url>
// 输出 JSON 行（RESULT {...}）；--channel chrome 用本机 Chrome，默认 Playwright 自带 Chromium。
import { chromium } from 'playwright';

const args = process.argv.slice(2);
const cmd = args[0];
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : def; };
const flag = (name) => args.includes(name);
const page_url = opt('--page');
const runs = Number(opt('--runs', '5'));
const channel = opt('--channel', null);
const headless = !flag('--headed');

function stats(values) {
  const s = [...values].sort((a, b) => a - b);
  return { n: s.length, median: s[Math.floor(s.length / 2)], worst: s[s.length - 1], best: s[0], all: s };
}
function out(obj) { console.log('RESULT ' + JSON.stringify(obj)); }

async function launch() {
  const b = await chromium.launch({ headless, channel: channel || undefined });
  const v = b.version();
  return { browser: b, version: v };
}

async function waitReady(page) {
  await page.waitForFunction(() => window.__spike && window.__spike.ready === true, null, { timeout: 120000 });
}

async function startup() {
  const { browser, version } = await launch();
  const warm = flag('--warm');
  const samples = [];
  let context = warm ? await browser.newContext() : null;
  for (let i = 0; i < runs + (warm ? 1 : 0); i++) {
    if (!warm) context = await browser.newContext();
    const page = await context.newPage();
    page.on('console', (m) => { if (m.type() === 'error') console.error('[console.error]', m.text()); });
    await page.goto(page_url, { waitUntil: 'commit' });
    await waitReady(page);
    const t = await page.evaluate(() => ({ ...window.__spike.timings, boot: window.__spike.boot, probe: window.__spike.probe }));
    const nav = await page.evaluate(() => { const e = performance.getEntriesByType('navigation')[0]; return e ? { responseEnd: e.responseEnd, domContentLoaded: e.domContentLoadedEventEnd } : null; });
    const sample = { run: i, warm, probe: t.probe, scriptStartMs: t.scriptStart, dotnetCreateMs: t.dotnetCreated, exportsReadyMs: t.exportsReady, mainDoneMs: t.mainDone, worldManagerReadyMs: t.bootDone, bootMs: t.boot.bootMs, transferBytes: t.resources.reduce((s, r) => s + r.transferSize, 0), decodedBytes: t.resources.reduce((s, r) => s + r.decodedBodySize, 0), resources: t.resources.length, nav };
    if (!(warm && i === 0)) samples.push(sample);
    await page.close();
    if (!warm) await context.close();
  }
  if (warm) await context.close();
  out({ scenario: 'startup', warm, browser: version, page: page_url, runs: samples.length, worldManagerReadyMs: stats(samples.map((s) => s.worldManagerReadyMs)), dotnetCreateMs: stats(samples.map((s) => s.dotnetCreateMs)), transferBytes: samples[0]?.transferBytes, decodedBytes: samples[0]?.decodedBytes, samples });
  await browser.close();
}

async function rebuild() {
  const { browser, version } = await launch();
  const page = await browser.newPage();
  await page.goto(page_url); await waitReady(page);
  const fixtures = opt('--fixtures', '100,300,1000').split(',');
  const inputs = Number(opt('--inputs', '5'));
  const repeats = Number(opt('--repeats', '5'));
  for (const n of fixtures) {
    const r = await page.evaluate(([f, i, k]) => window.__spike.rebuild(`fixtures/world-${f}.lwm1`, i, k), [n, inputs, repeats]);
    out({ scenario: 'rebuild', browser: version, entities: Number(n), ...r });
  }
  await browser.close();
}

async function wire() {
  const { browser, version } = await launch();
  const page = await browser.newPage();
  page.on('console', (m) => { if (m.type() === 'error') console.error('[console.error]', m.text()); });
  await page.goto(page_url); await waitReady(page);
  const ws = opt('--ws');
  const seconds = Number(opt('--seconds', '30'));
  const chats = Number(opt('--chats', '5'));
  const sampleEvery = Number(opt('--sample-seconds', '30'));
  await page.evaluate((u) => window.__spike.connect(u), ws);
  await page.waitForFunction(() => window.__spike.frames.length > 0, null, { timeout: 20000 });
  const first = await page.evaluate(() => window.__spike.frames[0]);
  out({ scenario: 'wire-first-frame', browser: version, ws, decoded: first.decoded, bytes: first.bytes, rawHead: first.rawHead, raw: first.raw });
  await page.waitForFunction(() => window.__spike.frames.some((f) => f.decoded.event), null, { timeout: 20000 });
  const firstEvent = await page.evaluate(() => window.__spike.frames.find((f) => f.decoded.event));
  out({ scenario: 'wire-first-event-frame', browser: version, ws, decoded: firstEvent.decoded, bytes: firstEvent.bytes, raw: firstEvent.raw });
  const roundtrips = [];
  for (let i = 0; i < chats; i++) {
    const rt = await page.evaluate((t) => window.__spike.sendChat(t), `hello-from-wasm-${i}`);
    roundtrips.push(rt);
    await page.waitForTimeout(200);
  }
  const sent = await page.evaluate(() => window.__spike.sent);
  out({ scenario: 'wire-chat', browser: version, sentFrames: sent.map((s) => ({ bytes: s.bytes, payloadBytes: s.payloadBytes, encodeMs: s.encodeMs, frame: s.frame })), roundtripMs: stats(roundtrips.map((r) => r.roundtripMs)), roundtrips });
  const timeline = [];
  const start = Date.now();
  while ((Date.now() - start) / 1000 < seconds) {
    await page.waitForTimeout(Math.min(sampleEvery, seconds) * 1000);
    const snap = await page.evaluate(() => ({ stats: window.__spike.frameStats(), gc: window.__spike.gc(), memoryBytes: window.__spike.memoryBytes(), errors: window.__spike.errors, closed: window.__spike.wire.closed }));
    snap.atSeconds = Math.round((Date.now() - start) / 1000);
    timeline.push(snap);
    out({ scenario: 'wire-sample', ...snap });
  }
  out({ scenario: 'wire-summary', browser: version, seconds, timeline });
  await browser.close();
}

async function interop() {
  const { browser, version } = await launch();
  const page = await browser.newPage();
  await page.goto(page_url); await waitReady(page);
  for (let i = 0; i < runs; i++) out({ scenario: 'interop', browser: version, run: i, results: await page.evaluate(() => window.__spike.interop(10000)) });
  await browser.close();
}

async function timers() {
  const { browser, version } = await launch();
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto(page_url); await waitReady(page);
  for (const kind of ['timeout', 'raf', 'worker']) {
    const r = await page.evaluate((k) => window.__spike.timerJitter(k, 50, 200), kind);
    out({ scenario: 'timer-jitter', browser: version, ...r });
  }
  if (flag('--background')) {
    // 页面切到后台：另开一页抢前台，观察 setTimeout 被节流到什么程度、WebSocket 帧是否照常到达（积压在哪一侧）。
    const ws = opt('--ws', null);
    if (ws) { await page.evaluate((u) => window.__spike.connect(u), ws); await page.waitForFunction(() => window.__spike.frames.length > 5); }
    const before = await page.evaluate(() => ({ frames: window.__spike.frames.length, t: performance.now(), hidden: document.hidden }));
    // 同一 context 里开新标签页（browser.newPage() 会另开窗口，原页不会变 hidden）
    const other = await context.newPage();
    await other.goto('about:blank');
    await other.bringToFront();
    const seconds = Number(opt('--background-seconds', '45'));
    const jitter = page.evaluate((s) => window.__spike.timerJitter('timeout', 50, Math.floor(s * 1000 / 50)), seconds);
    await other.waitForTimeout(seconds * 1000 + 2000);
    const r = await jitter;
    const during = await page.evaluate(() => ({ frames: window.__spike.frames.length, t: performance.now(), hidden: document.hidden, stats: window.__spike.frameStats() }));
    await page.bringToFront();
    await page.waitForTimeout(1000);
    const after = await page.evaluate(() => ({ frames: window.__spike.frames.length, t: performance.now(), hidden: document.hidden }));
    out({ scenario: 'timer-jitter-background', browser: version, backgroundSeconds: seconds, timer: r, framesBefore: before.frames, framesDuring: during.frames - before.frames, expectedFramesAt20Hz: Math.round((during.t - before.t) / 50), hiddenDuring: during.hidden, wsIntervalMsMaxDuring: during.stats.intervalMsMax, framesAfterForeground1s: after.frames - during.frames });
    await other.close();
  }
  await browser.close();
}

async function draw() {
  const { browser, version } = await launch();
  const page = await browser.newPage();
  await page.goto(page_url); await waitReady(page);
  for (const mode of ['packed', 'json']) out({ scenario: 'draw', browser: version, ...(await page.evaluate((m) => window.__spike.drawFrames(100, 120, m), mode)) });
  for (let i = 0; i < runs; i++) out({ scenario: 'input-to-paint', browser: version, run: i, ...(await page.evaluate(() => window.__spike.inputToPaint(20))) });
  await browser.close();
}

async function worker() {
  // A1-5 后半：整个运行时挪进 Web Worker（worker.html）。
  const { browser, version } = await launch();
  for (let i = 0; i < runs; i++) {
    const context = await browser.newContext();
    const page = await context.newPage();
    page.on('console', (m) => { if (m.type() === 'error') console.error('[console.error]', m.text()); });
    await page.goto(page_url, { waitUntil: 'commit' });
    await page.waitForFunction(() => window.__spikeWorker && (window.__spikeWorker.ready || window.__spikeWorker.errors.length > 0), null, { timeout: 120000 });
    const r = await page.evaluate(() => window.__spikeWorker);
    out({ scenario: 'worker', browser: version, run: i, ready: r.ready, errors: r.errors, hostSawReadyAtMs: r.hostSawReadyAtMs, ...(r.worker || {}) });
    await context.close();
  }
  await browser.close();
}

const handlers = { startup, rebuild, wire, interop, timers, draw, worker };
if (!handlers[cmd] || !page_url) { console.error('usage: node tools/measure.mjs <startup|rebuild|wire|interop|timers|draw> --page <url> [...]'); process.exit(2); }
await handlers[cmd]();
