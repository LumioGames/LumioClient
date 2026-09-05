// CL-1 探针：把 results/*.jsonl 的 RESULT 行汇总成 Markdown 表（报告直接引用；原始行仍在 results/）。
// 用法：node tools/summarize.mjs results
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
const dir = process.argv[2] || 'results';
const f1 = (v) => (typeof v === 'number' ? v.toFixed(1) : String(v));
const f2 = (v) => (typeof v === 'number' ? v.toFixed(2) : String(v));
const rows = [];
for (const f of readdirSync(dir).filter((x) => x.endsWith('.jsonl')).sort()) {
  for (const line of readFileSync(join(dir, f), 'utf8').split('\n')) {
    if (line.startsWith('RESULT ')) rows.push({ file: f, ...JSON.parse(line.slice(7)) });
  }
}
const by = (s) => rows.filter((r) => r.scenario === s);
console.log('## startup');
console.log('| 变体 / 文件 | 缓存 | 次数 | WorldManager 可用 中位 / 最差 ms | dotnet.create 中位 / 最差 ms | 实际传输字节 | 解压后字节 |');
console.log('|---|---|---|---|---|---|---|');
for (const r of by('startup')) console.log(`| ${r.file} | ${r.warm ? '热' : '空'} | ${r.runs} | ${f1(r.worldManagerReadyMs.median)} / ${f1(r.worldManagerReadyMs.worst)} | ${f1(r.dotnetCreateMs.median)} / ${f1(r.dotnetCreateMs.worst)} | ${r.transferBytes} | ${r.decodedBytes} |`);
console.log('\n## rebuild');
console.log('| 文件 | 实体 | 输入 | 次数 | Create 中位 ms | Apply 中位 ms | 总 中位 / 最差 / 最好 ms | 占 50 ms 帧预算 | 哈希 |');
console.log('|---|---|---|---|---|---|---|---|---|');
for (const r of by('rebuild')) console.log(`| ${r.file} | ${r.entities} | ${r.inputs} | ${r.repeats} | ${f2(r.createMsMedian)} | ${f2(r.applyMsMedian)} | ${f2(r.totalMsMedian)} / ${f2(r.totalMsWorst)} / ${f2(r.totalMsBest)} | ${(r.totalMsMedian / 50 * 100).toFixed(1)} % (最差 ${(r.totalMsWorst / 50 * 100).toFixed(1)} %) | ${r.hashes.join(' ')} |`);
console.log('\n## interop (run 0 of each file)');
for (const r of by('interop').filter((x) => x.run === 0)) { console.log(`file ${r.file}`); for (const x of r.results) console.log(`- ${x.name}: ${x.perCallUs !== undefined ? x.perCallUs.toFixed(2) + ' µs/call' : JSON.stringify(x)}`); }
console.log('\n## timers');
for (const r of by('timer-jitter')) console.log(`- ${r.kind}: interval 中位 ${f2(r.intervalMsMedian)} ms（min ${f2(r.intervalMsMin)} / max ${f2(r.intervalMsMax)}），抖动 中位 ${f2(r.jitterMsMedian)} / P95 ${f2(r.jitterMsP95)} / max ${f2(r.jitterMsMax)} ms（${r.samples} 样本）`);
for (const r of by('timer-jitter-background')) console.log(`- background ${r.backgroundSeconds}s: setTimeout(50) interval 中位 ${f2(r.timer.intervalMsMedian)} ms max ${f2(r.timer.intervalMsMax)} ms（${r.timer.samples} 样本）；WS 帧 during=${r.framesDuring} expected≈${r.expectedFramesAt20Hz} maxGap=${f1(r.wsIntervalMsMaxDuring)} ms；回前台 1 s 内又收 ${r.framesAfterForeground1s} 帧；hidden=${r.hiddenDuring}`);
console.log('\n## draw / input-to-paint');
for (const r of by('draw')) console.log(`- draw ${r.mode} ${r.entities} 实体 × ${r.frames} 帧：每帧 中位 ${f2(r.msMedian)} / P95 ${f2(r.msP95)} / max ${f2(r.msMax)} ms`);
const itp = by('input-to-paint');
if (itp.length) { const med = itp.map((r) => r.inputToPaintMsMedian); const worst = itp.map((r) => r.inputToPaintMsWorst); console.log(`- input→predicted→paint：各轮中位 ${med.map(f2).join(' / ')} ms，各轮最差 ${worst.map(f2).join(' / ')} ms；LocalWrite(C#) 首轮样本 ${itp[0].samples.slice(0,3).map((s) => f2(s.localWriteMs)).join(', ')} ms`); }
console.log('\n## wire');
for (const r of by('wire-first-frame')) console.log(`- ${r.file} first frame: ${r.bytes} bytes ok=${r.decoded.ok} decodeMs=${r.decoded.decodeMs} head=${r.rawHead.slice(0, 90)}…`);
for (const r of by('wire-chat')) console.log(`- ${r.file} chat roundtrip ms: 中位 ${f1(r.roundtripMs.median)} 最差 ${f1(r.roundtripMs.worst)} 最好 ${f1(r.roundtripMs.best)}（${r.roundtripMs.n} 条；sent ${r.sentFrames[0].bytes} B，payload ${r.sentFrames[0].payloadBytes} B，encodeMs 首条 ${r.sentFrames[0].encodeMs} 之后 ${r.sentFrames.slice(1).map((s) => s.encodeMs).join('/')}）`);
for (const r of by('wire-summary')) { const last = r.timeline[r.timeline.length - 1]; console.log(`- ${r.file} soak ${r.seconds}s: frames=${last.stats.frames} ok=${last.stats.ok} events=${last.stats.events} decode 中位 ${last.stats.decodeMsMedian} P95 ${last.stats.decodeMsP95} max ${last.stats.decodeMsMax} ms；到达间隔 中位 ${f1(last.stats.intervalMsMedian)} P95 ${f1(last.stats.intervalMsP95)} max ${f1(last.stats.intervalMsMax)} ms；bytes=${last.stats.bytesTotal}；wasm memory ${last.memoryBytes} B；GC.GetTotalMemory ${last.gc.totalMemory}`); for (const t of r.timeline) console.log(`  - t=${t.atSeconds}s frames=${t.stats.frames} decodeMedian=${t.stats.decodeMsMedian} P95=${t.stats.decodeMsP95} intervalP95=${f1(t.stats.intervalMsP95)} mem=${t.memoryBytes} gcTotal=${t.gc.totalMemory}`); }
console.log('\n## js baseline');
for (const r of by('js-chat-page')) console.log(`- 纯 JS 聊天页 ${r.page}: ready 中位 ${f1(r.readyMs.median)} / 最差 ${f1(r.readyMs.worst)} ms；DOMContentLoaded 中位 ${f1(r.domContentLoadedMs.median)} ms；传输 ${r.transferBytes} B（解压 ${r.decodedBytes} B，${r.resources} 个资源）`);
for (const r of by('js-game-page-connect')) console.log(`- Game 聊天页连宿主 ${r.ws}: 页面就绪 中位 ${f1(r.pageReadyMs.median)} ms；连接→首帧 中位 ${f1(r.connectToFirstFrameMs.median)} / 最差 ${f1(r.connectToFirstFrameMs.worst)} ms；导航→首帧 中位 ${f1(r.firstFrameMs.median)} ms`);
console.log('\n## worker');
for (const r of by('worker')) console.log(`- ${r.file}: isWorker=${r.isWorker} ready=${r.ready} ownerThread=${r.boot?.ownerThread} dotnet.create ${f1(r.dotnetCreateMs)} ms WorldManager ${f1(r.worldManagerReadyMs)} ms decode ok=${r.decoded?.ok} rebuild(100) 中位 ${f2(r.rebuild?.totalMsMedian)} ms hash ${r.rebuild?.hashes} errors=${JSON.stringify(r.errors)}`);
