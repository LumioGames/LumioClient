// CL-1 探针：统计 publish 产物体积。用法：node tools/sizes.mjs <publish/<variant>/wwwroot>
// 按类别（dotnet.js 族 / dotnet.native.wasm / Runtime 程序集 / BCL 程序集 / ICU / 其他）给未压缩、gzip -9、brotli -q 11 三个数。
import { readdirSync, statSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync, brotliCompressSync, constants } from 'node:zlib';

const root = process.argv[2];
const fw = join(root, '_framework');
const rows = [];
for (const name of readdirSync(fw)) {
  if (name.endsWith('.gz') || name.endsWith('.br') || name.endsWith('.pdb')) continue;
  const file = join(fw, name);
  const st = statSync(file);
  if (!st.isFile()) continue;
  const buf = readFileSync(file);
  const gz = gzipSync(buf, { level: 9 }).length;
  const br = brotliCompressSync(buf, { params: { [constants.BROTLI_PARAM_QUALITY]: 11, [constants.BROTLI_PARAM_SIZE_HINT]: buf.length } }).length;
  let category = 'other';
  if (/^dotnet(\..+)?\.js$/.test(name) || /^dotnet\.[a-z0-9]+\.js$/.test(name) || name.startsWith('dotnet.runtime') || name.startsWith('dotnet.boot')) category = 'dotnet.js';
  else if (name.startsWith('dotnet.native') && name.endsWith('.wasm')) category = 'dotnet.native.wasm';
  else if (name.startsWith('Lumio.GameRuntime.')) category = 'runtime-assemblies';
  else if (name.startsWith('Lumio.Client.Spike')) category = 'spike-assemblies';
  else if (name.startsWith('icudt')) category = 'icu';
  else if (name.endsWith('.wasm') || name.endsWith('.dll') || name.endsWith('.webcil')) category = 'bcl-assemblies';
  else if (name.endsWith('.js')) category = 'dotnet.js';
  else if (name.endsWith('.json') || name.endsWith('.dat')) category = 'config-data';
  rows.push({ name, category, raw: buf.length, gz, br });
}
const byCat = {};
for (const r of rows) {
  byCat[r.category] ??= { files: 0, raw: 0, gz: 0, br: 0 };
  byCat[r.category].files++; byCat[r.category].raw += r.raw; byCat[r.category].gz += r.gz; byCat[r.category].br += r.br;
}
const total = rows.reduce((a, r) => ({ files: a.files + 1, raw: a.raw + r.raw, gz: a.gz + r.gz, br: a.br + r.br }), { files: 0, raw: 0, gz: 0, br: 0 });
console.log('SIZES ' + JSON.stringify({ root, total, byCategory: byCat, files: rows.sort((a, b) => b.raw - a.raw) }));
