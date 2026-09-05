// 在 Worker 线程里起 .NET：dotnet.create → Boot（WorldManager.Start 绑定 worker 线程）→ 解一帧 → 重建基准。
// 文件用 .mjs 后缀是为了不被 StaticWebAssetFingerprintPattern(*.js) 改名，主线程能按固定名 new Worker()。
// 注意：dotnet.js 在 Worker 里按 `globalThis.onmessage` 是否已被赋值来区分「被运行时自己起的 pthread worker」与「独立 sidecar」
// （源码：`"function"!=typeof importScripts||globalThis.onmessage||(globalThis.dotnetSidecar=!0)`）。所以这里必须用 addEventListener，
// 不能写 self.onmessage = …，否则 dotnet.create() 会把自己当 pthread 等主线程消息，永远不返回（本卡第一次就踩了这个坑）。
self.addEventListener('message', async (ev) => {
  const { dotnetUrl, fixture } = ev.data;
  const post = (kind, extra) => self.postMessage({ kind, ...extra });
  try {
    const t0 = performance.now();
    const { dotnet } = await import(dotnetUrl);
    const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
    const tCreated = performance.now();
    const exports = await getAssemblyExports(getConfig().mainAssemblyName);
    await runMain();
    const api = exports.Lumio.Client.Spike.RuntimeWasm.SpikeExports;
    const probe = JSON.parse(api.Probe());
    const boot = JSON.parse(api.Boot('c-worker', 'Worker01'));
    const tBoot = performance.now();
    // 一帧真实形状的空 Delta（Runtime codec 校验路径）；真实包由主线程 WebSocket 拿到后 postMessage 进来也可以，这里先证明 codec 在 worker 里可用
    const decoded = JSON.parse(api.OnFrame('{"messageType":"Delta","tickId":1,"revision":1,"changedBlocks":[]}'));
    const bytes = new Uint8Array(await (await fetch(fixture)).arrayBuffer());
    const samples = JSON.parse(api.Rebuild(bytes, 5, 7));
    const totals = samples.map((s) => s.totalMs).sort((a, b) => a - b);
    let localWriteMs = api.LocalWrite('worker-write');
    post('ready', {
      isWorker: typeof WorkerGlobalScope !== 'undefined' && self instanceof WorkerGlobalScope,
      dotnetCreateMs: tCreated - t0, worldManagerReadyMs: tBoot - t0, probe, boot, decoded, localWriteMs,
      rebuild: { entities: 100, totalMsMedian: totals[Math.floor(totals.length / 2)], totalMsWorst: totals[totals.length - 1], hashes: [...new Set(samples.map((s) => s.hash))] },
    });
  } catch (e) {
    post('error', { text: String(e && e.stack || e) });
  }
});
