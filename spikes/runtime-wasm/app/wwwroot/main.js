// CL-1 探针：JS 只做三件事——起 .NET、搬 WebSocket 字节、画 Canvas。协议解析全部在 C#（Runtime 程序集）。
// 机器可读证据：window.__spike（timings / frames / results）。
const T0 = performance.now();
import { dotnet } from './_framework/dotnet.js';

const timings = { scriptStart: T0, timeOrigin: performance.timeOrigin };
const logEl = document.getElementById('log');
const statusEl = document.getElementById('status');
const wireEl = document.getElementById('wire');
function log(line) {
  console.log('[spike] ' + line);
  logEl.textContent = (logEl.textContent + line + '\n').split('\n').slice(-60).join('\n');
}

const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
timings.dotnetCreated = performance.now();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
// [JSExport] 按命名空间挂在 exports 下：Lumio.Client.Spike.RuntimeWasm.SpikeExports
const api = exports.Lumio.Client.Spike.RuntimeWasm.SpikeExports;
timings.exportsReady = performance.now();
await runMain();
timings.mainDone = performance.now();

const params = new URLSearchParams(location.search);
const connectionId = params.get('connectionId') || 'c-browser';
const selfName = params.get('login') || 'Browser01';

const spike = {
  timings,
  probe: null,
  boot: null,
  frames: [],
  sent: [],
  roundtrips: [],
  errors: [],
  wire: { url: null, connected: false, closed: false },
  exports,
};
window.__spike = spike;

spike.probe = JSON.parse(api.Probe());
timings.probeDone = performance.now();
spike.boot = JSON.parse(api.Boot(connectionId, selfName));
timings.bootDone = performance.now();
timings.resources = performance.getEntriesByType('resource')
  .filter((e) => e.name.includes('/_framework/'))
  .map((e) => ({ name: e.name.split('/_framework/')[1], transferSize: e.transferSize, encodedBodySize: e.encodedBodySize, decodedBodySize: e.decodedBodySize, durationMs: e.duration }));
statusEl.textContent = `ready · self=${spike.boot.self} · dotnet.create ${(timings.dotnetCreated - T0).toFixed(1)} ms · WorldManager ready @ ${timings.bootDone.toFixed(1)} ms`;
log(`probe ${JSON.stringify(spike.probe)}`);
log(`boot ${JSON.stringify(spike.boot)}`);

// ---------- wire: bytes in → C# decode; C# encode → bytes out ----------
let socket = null;
let pending = null;
spike.connect = (url) => new Promise((resolve, reject) => {
  spike.wire.url = url;
  socket = new WebSocket(url);
  const timer = setTimeout(() => reject(new Error('websocket open timeout')), 15000);
  socket.addEventListener('open', () => {
    clearTimeout(timer);
    socket.send(JSON.stringify({ connectionId }));
    spike.wire.connected = true;
    wireEl.textContent = `wire: connected ${url} as ${connectionId}`;
    resolve(true);
  });
  socket.addEventListener('error', () => { clearTimeout(timer); spike.errors.push('websocket error'); reject(new Error('websocket error')); });
  socket.addEventListener('close', () => { spike.wire.closed = true; wireEl.textContent += ' · closed'; });
  socket.addEventListener('message', (ev) => {
    const received = performance.now();
    const raw = String(ev.data);
    const decoded = JSON.parse(api.OnFrame(raw));
    const record = { t: received, bytes: raw.length, decoded, rawHead: raw.slice(0, 160) };
    if (spike.frames.length < 400) record.raw = raw;
    spike.frames.push(record);
    if (spike.frames.length > 20000) spike.frames.splice(0, 10000);
    if (decoded.event && pending && decoded.event.text.endsWith(pending.text)) {
      const rt = { text: pending.text, sentAt: pending.sentAt, receivedAt: received, roundtripMs: received - pending.sentAt, decodeMs: decoded.decodeMs, event: decoded.event };
      spike.roundtrips.push(rt);
      pending.resolve(rt);
      pending = null;
    }
  });
});
spike.sendChat = (text) => new Promise((resolve, reject) => {
  const encoded = JSON.parse(api.EncodeChat(text));
  if (encoded.error) { reject(new Error(encoded.error)); return; }
  const frame = JSON.stringify(encoded.envelope);
  const sentAt = performance.now();
  pending = { text, sentAt, resolve };
  spike.sent.push({ text, sentAt, bytes: frame.length, payloadBytes: encoded.payloadBytes, encodeMs: encoded.encodeMs, frame });
  socket.send(frame);
  setTimeout(() => { if (pending && pending.text === text) { pending = null; reject(new Error('echo timeout for ' + text)); } }, 10000);
});
spike.frameStats = () => {
  const f = spike.frames;
  const decodeMs = f.map((x) => x.decoded.decodeMs).sort((a, b) => a - b);
  const ok = f.filter((x) => x.decoded.ok).length;
  const events = f.filter((x) => x.decoded.event).length;
  const intervals = [];
  for (let i = 1; i < f.length; i++) intervals.push(f[i].t - f[i - 1].t);
  intervals.sort((a, b) => a - b);
  const pct = (arr, p) => arr.length ? arr[Math.min(arr.length - 1, Math.floor(arr.length * p))] : null;
  return { frames: f.length, ok, events, decodeMsMedian: pct(decodeMs, 0.5), decodeMsP95: pct(decodeMs, 0.95), decodeMsMax: decodeMs[decodeMs.length - 1] ?? null, intervalMsMedian: pct(intervals, 0.5), intervalMsP95: pct(intervals, 0.95), intervalMsMax: intervals[intervals.length - 1] ?? null, bytesTotal: f.reduce((s, x) => s + x.bytes, 0) };
};

// ---------- rebuild benchmark (fixture bytes fetched by JS, handed to C# as byte[]) ----------
spike.rebuild = async (fixtureUrl, inputs, repeats) => {
  const bytes = new Uint8Array(await (await fetch(fixtureUrl, { cache: 'no-store' })).arrayBuffer());
  const gcBefore = JSON.parse(api.Gc());
  const t = performance.now();
  const samples = JSON.parse(api.Rebuild(bytes, inputs, repeats));
  const wallMs = performance.now() - t;
  const gcAfter = JSON.parse(api.Gc());
  const totals = samples.map((s) => s.totalMs).sort((a, b) => a - b);
  const result = { fixture: fixtureUrl, bytes: bytes.length, inputs, repeats, livePlayers: samples[0]?.livePlayers, hashes: [...new Set(samples.map((s) => s.hash))], totalMsMedian: totals[Math.floor(totals.length / 2)], totalMsWorst: totals[totals.length - 1], totalMsBest: totals[0], createMsMedian: samples.map((s) => s.createMs).sort((a, b) => a - b)[Math.floor(samples.length / 2)], applyMsMedian: samples.map((s) => s.applyMs).sort((a, b) => a - b)[Math.floor(samples.length / 2)], wallMs, gcBefore, gcAfter, samples, wasmMemoryBytes: spike.memoryBytes() };
  log(`rebuild ${fixtureUrl} median ${result.totalMsMedian.toFixed(2)} ms worst ${result.totalMsWorst.toFixed(2)} ms hash ${result.hashes.join('|')}`);
  return result;
};
spike.memoryBytes = () => {
  try { return dotnet.instance?.Module?.HEAPU8?.buffer?.byteLength ?? null; } catch { return null; }
};
spike.gc = () => JSON.parse(api.Gc());

// ---------- interop micro-benchmarks ----------
spike.interop = (iterations = 10000) => {
  const bench = (name, fn) => {
    fn(0); fn(1);
    const t = performance.now();
    for (let i = 0; i < iterations; i++) fn(i);
    const total = performance.now() - t;
    return { name, iterations, totalMs: total, perCallUs: (total / iterations) * 1000 };
  };
  const str100 = 'x'.repeat(100);
  const results = [
    bench('Ping(int)', (i) => api.Ping(i | 0)),
    bench('EchoDouble(double)', (i) => api.EchoDouble(i + 0.5)),
    bench('EchoNumber(long as Number)', (i) => api.EchoNumber(i)),
    bench('EchoBigInt(long as BigInt)', (i) => api.EchoBigInt(BigInt(i))),
    bench('EchoString(100 chars)', () => api.EchoString(str100)),
    bench('DiffJson(100)', (i) => api.DiffJson(100, i | 0)),
    bench('DiffPacked(100) int[]', (i) => api.DiffPacked(100, i | 0)),
    bench('DiffJson(100)+JSON.parse', (i) => JSON.parse(api.DiffJson(100, i | 0))),
  ];
  results.push({ name: 'DiffJson(100) bytes', bytes: api.DiffJson(100, 1).length });
  results.push({ name: 'DiffPacked(100) ints', ints: api.DiffPacked(100, 1).length });
  let bigOk = true;
  try { bigOk = api.EchoBigInt(9007199254740993n) === 9007199254740994n; } catch (e) { bigOk = 'throws: ' + e.message; }
  let numOk = null;
  try { numOk = api.EchoNumber(9007199254740993); } catch (e) { numOk = 'throws: ' + e.message; }
  results.push({ name: 'long beyond 2^53', bigIntExact: bigOk, numberPath: String(numOk) });
  return results;
};

// ---------- canvas draw of a presentation diff ----------
const canvas = document.getElementById('stage');
const ctx = canvas.getContext('2d');
spike.drawFrames = (entities = 100, frames = 120, mode = 'packed') => new Promise((resolve) => {
  const perFrame = [];
  let n = 0;
  const step = () => {
    const t = performance.now();
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (mode === 'json') {
      const diff = JSON.parse(api.DiffJson(entities, n));
      for (const c of diff.continued) ctx.fillRect(c.x, c.y, 6, 6);
    } else {
      const data = api.DiffPacked(entities, n);
      for (let i = 0; i < data.length; i += 4) ctx.fillRect(data[i + 2], data[i + 3], 6, 6);
    }
    perFrame.push(performance.now() - t);
    if (++n < frames) requestAnimationFrame(step); else {
      perFrame.sort((a, b) => a - b);
      resolve({ entities, frames, mode, msMedian: perFrame[Math.floor(perFrame.length / 2)], msP95: perFrame[Math.floor(perFrame.length * 0.95)], msMax: perFrame[perFrame.length - 1] });
    }
  };
  requestAnimationFrame(step);
});

// ---------- input → predicted world update → canvas paint latency ----------
spike.inputToPaint = (samples = 20) => new Promise((resolve) => {
  const out = [];
  let i = 0;
  const one = () => {
    const tInput = performance.now();
    const localMs = api.LocalWrite('predicted-' + i);
    const tPredicted = performance.now();
    ctx.fillStyle = i % 2 ? '#c33' : '#33c';
    ctx.fillRect(10 + (i * 7) % 700, 10, 8, 8);
    requestAnimationFrame(() => {
      const tPainted = performance.now();
      out.push({ localWriteMs: localMs, inputToPredictedMs: tPredicted - tInput, inputToPaintMs: tPainted - tInput });
      if (++i < samples) setTimeout(one, 30); else {
        const sorted = out.map((o) => o.inputToPaintMs).sort((a, b) => a - b);
        resolve({ samples: out, inputToPaintMsMedian: sorted[Math.floor(sorted.length / 2)], inputToPaintMsWorst: sorted[sorted.length - 1] });
      }
    });
  };
  one();
});

// ---------- 20 Hz timer jitter: setTimeout / requestAnimationFrame / Worker ----------
spike.timerJitter = (kind = 'timeout', periodMs = 50, samples = 200) => new Promise((resolve) => {
  const stamps = [];
  const finish = () => {
    const d = [];
    for (let i = 1; i < stamps.length; i++) d.push(stamps[i] - stamps[i - 1]);
    const jitter = d.map((x) => Math.abs(x - periodMs)).sort((a, b) => a - b);
    d.sort((a, b) => a - b);
    resolve({ kind, periodMs, samples: d.length, intervalMsMedian: d[Math.floor(d.length / 2)], intervalMsMin: d[0], intervalMsMax: d[d.length - 1], jitterMsMedian: jitter[Math.floor(jitter.length / 2)], jitterMsP95: jitter[Math.floor(jitter.length * 0.95)], jitterMsMax: jitter[jitter.length - 1], hidden: document.hidden });
  };
  if (kind === 'timeout') {
    const tick = () => { stamps.push(performance.now()); if (stamps.length <= samples) setTimeout(tick, periodMs); else finish(); };
    setTimeout(tick, periodMs);
  } else if (kind === 'raf') {
    let last = performance.now();
    const tick = (now) => { if (now - last >= periodMs - 1) { stamps.push(now); last = now; } if (stamps.length <= samples) requestAnimationFrame(tick); else finish(); };
    requestAnimationFrame(tick);
  } else if (kind === 'worker') {
    const src = `let p=${periodMs};setInterval(()=>postMessage(performance.now()),p);`;
    const worker = new Worker(URL.createObjectURL(new Blob([src], { type: 'text/javascript' })));
    worker.onmessage = () => { stamps.push(performance.now()); if (stamps.length > samples) { worker.terminate(); finish(); } };
  }
});

// auto-connect when ?ws= is present (same convention as modules/web/hello: ?ws=ws://host:port/)
const ws = params.get('ws');
if (ws) {
  spike.connect(ws).catch((e) => { spike.errors.push(String(e.message || e)); wireEl.textContent = 'wire: ' + e.message; });
}
spike.ready = true;
timings.readyAt = performance.now();
