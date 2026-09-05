// CL-1 探针：零依赖静态文件服务。用法：node tools/static-server.mjs --root <dir> [--port 0] [--no-store] [--coop-coep]
// 打印 STATIC_READY {"port":N}。--no-store 让每次加载都是空缓存（测冷启动）；--coop-coep 发 Cross-Origin-Opener/Embedder-Policy（多线程 wasm 需要）。
import http from 'node:http';
import { createReadStream, statSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';

const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : def; };
const root = resolve(opt('--root', '.'));
const port = Number(opt('--port', '0'));
const noStore = args.includes('--no-store');
const coopCoep = args.includes('--coop-coep');
const mime = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.wasm': 'application/wasm', '.css': 'text/css; charset=utf-8',
  '.dat': 'application/octet-stream', '.lwm1': 'application/octet-stream', '.pdb': 'application/octet-stream',
  '.dll': 'application/octet-stream', '.blat': 'application/octet-stream', '.ico': 'image/x-icon', '.svg': 'image/svg+xml',
  '.map': 'application/json', '.txt': 'text/plain; charset=utf-8', '.webcil': 'application/octet-stream',
};

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  let path = normalize(decodeURIComponent(url.pathname));
  if (path.endsWith('/')) path += 'index.html';
  const file = join(root, path);
  if (!file.startsWith(root)) { res.writeHead(403); res.end(); return; }
  let st;
  try { st = statSync(file); } catch { res.writeHead(404); res.end('not found'); return; }
  if (st.isDirectory()) { res.writeHead(301, { Location: url.pathname + '/' }); res.end(); return; }
  const headers = { 'Content-Type': mime[extname(file)] || 'application/octet-stream', 'Content-Length': st.size };
  headers['Cache-Control'] = noStore ? 'no-store' : 'public, max-age=3600';
  if (coopCoep) { headers['Cross-Origin-Opener-Policy'] = 'same-origin'; headers['Cross-Origin-Embedder-Policy'] = 'require-corp'; }
  res.writeHead(200, headers);
  createReadStream(file).pipe(res);
});
server.listen(port, '127.0.0.1', () => {
  console.log('STATIC_READY ' + JSON.stringify({ port: server.address().port, root, noStore, coopCoep }));
});
process.stdin.on('data', (d) => { if (String(d).trim() === 'shutdown') process.exit(0); });
