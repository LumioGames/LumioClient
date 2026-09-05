// CL-1 探针（A1-5 后半）：把整个 .NET 运行时 + WorldManager 挪进一个 Web Worker，主线程只收结果。
// Worker 里没有 importmap，所以主线程把 importmap 里 dotnet.js 的指纹名解析出来发给 worker 动态 import。
const statusEl = document.getElementById('status');
const logEl = document.getElementById('log');
const log = (l) => { logEl.textContent += l + '\n'; console.log('[worker-host] ' + l); };
// dotnet.js 的指纹名：从 index.html 的 importmap 里读（Worker 里没有 importmap，只能把绝对 URL 传进去）
const indexHtml = await (await fetch('./index.html', { cache: 'no-store' })).text();
const importmap = JSON.parse(indexHtml.match(/<script type="importmap">([\s\S]*?)<\/script>/)[1]);
const dotnetUrl = new URL(importmap.imports['./_framework/dotnet.js'], location.href).href;
const result = { ready: false, worker: null, errors: [] };
window.__spikeWorker = result;
const t0 = performance.now();
const worker = new Worker(new URL('./worker-runtime.mjs', location.href), { type: 'module' });
worker.onmessage = (ev) => {
  const m = ev.data;
  if (m.kind === 'log') { log(m.text); return; }
  if (m.kind === 'error') { result.errors.push(m.text); statusEl.textContent = 'error: ' + m.text; log('ERROR ' + m.text); return; }
  if (m.kind === 'ready') {
    result.worker = m; result.ready = true; result.hostSawReadyAtMs = performance.now() - t0;
    statusEl.textContent = `worker ready · self=${m.boot.self} · ownerThread=${m.boot.ownerThread} · rebuild(100) median ${m.rebuild.totalMsMedian.toFixed(2)} ms`;
    log(JSON.stringify(m));
  }
};
worker.onerror = (e) => { result.errors.push(e.message); statusEl.textContent = 'worker error: ' + e.message; };
worker.postMessage({ kind: 'start', dotnetUrl, fixture: new URL('./fixtures/world-100.lwm1', location.href).href });
