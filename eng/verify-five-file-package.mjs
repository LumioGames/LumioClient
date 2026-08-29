#!/usr/bin/env node
/**
 * verify-five-file-package — 给五文件恢复包的清单/审计记录补机器校验。
 * 用法:node eng/verify-five-file-package.mjs [仓库根目录]   (省略参数时取本脚本上级目录)
 *
 * 为什么需要这道闸门:manifest 与 audit 记录的 bytes/SHA-256 是审计证据,但被记录的对象
 * (三个规划 Markdown + manifest 自身)是普通仓库文件,任何人改一个字它们就失真。
 * 2026-08-29 之前全仓没有任何检查引用这些值(repository-policy 的 sha256sum -c 只覆盖
 * docs/architecture/.baseline.sha256),于是 T-00006 的一次合法修订让记录静默失真两天——
 * CI 全绿,证据变谎言。审计记录缺机器守护本身就是缺陷,这里补上。
 *
 * 为什么不是一个 dotnet 测试:被守护对象是纯文档,与 .NET 编译无关;放在 node 侧可以和
 * spec-lint / verify-sdk-pin 一起在 README policy job 里跑,不受 .NET 工具链状态影响。
 *
 * 校验项(本注释是该脚本能力清单的单一权威):
 *  1. 归档目录内的文件集合必须与 manifest 的 EXPECTED ARCHIVE ROOT 逐名一致,不多不少。
 *  2. manifest FILES 段记录的 bytes/sha256 必须与实际文件一致。manifest 与 audit 两项
 *     不记哈希:manifest 记自身是自引用,记 audit 则与「audit 记 manifest」构成循环
 *     (改一边另一边永远追不上)。记了就报错——这两处留空是刻意的,不是遗漏。
 *  3. audit files[] 记录的 bytes/sha256 必须与实际文件一致,且必须与 manifest 记录一致;
 *     audit 自身条目必须保持 bytes/sha256 为 null 并带 note(自引用约定)。
 *     audit 自身的字节没有任何地方记录哈希——它由本脚本逐项复核内容来守护,不靠冻结摘要。
 *  4. manifest COUNTS 段、audit counts 与从三个规划文件重算的计数三方必须一致。
 *  5. 归档目录内不得出现 .md/.txt/.json 以外的文件(production source files = 0 的实质)。
 *  6. 仓库侧换行必须是 LF(根 .gitattributes 的 `*.md text eol=lf`),且 audit 的
 *     line_endings_lf_in_repository 检查结论必须与实际一致——CRLF 是 Workflow 附件那一侧。
 */
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { join, extname, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export const ARCHIVE_DIR = 'docs/LumioClient_five_requested_files'
export const DESIGN_NAME = '2026-08-27-client-framework-scaffolding-design.md'
export const FOUNDATION_NAME = '2026-08-27-client-foundation-implementation.md'
export const VERTICAL_NAME = '2026-08-27-client-vertical-slice-implementation.md'
export const MANIFEST_NAME = 'LumioClient_framework_scaffolding_manifest.txt'
export const AUDIT_NAME = 'LumioClient_framework_scaffolding_audit.json'
export const LINE_ENDING_CHECK_ID = 'line_endings_lf_in_repository'

/** 归档内允许的扩展名;其余一律计为 production source file。 */
export const ALLOWED_EXTENSIONS = new Set(['.md', '.txt', '.json'])

/** manifest COUNTS 段的键 → audit counts 的键。 */
export const COUNT_KEYS = [
  { manifest: 'design modules', audit: 'design_modules', label: 'design modules' },
  { manifest: 'foundation plan tasks', audit: 'foundation_tasks', label: 'foundation plan tasks' },
  { manifest: 'vertical plan tasks', audit: 'vertical_slice_tasks', label: 'vertical plan tasks' },
  { manifest: 'production source files in archive', audit: 'production_source_files', label: 'production source files' },
]

/** 把 manifest 切成 `标题 → 正文行` 的段落表(标题行下面紧跟一行 `---`)。 */
function splitSections(text) {
  const lines = text.split('\n')
  const sections = new Map()
  let current = null
  for (let i = 0; i < lines.length; i++) {
    if (/^[A-Z][A-Z ]*$/.test(lines[i]) && /^-+$/.test(lines[i + 1] ?? '')) {
      current = lines[i]
      sections.set(current, [])
      i++
      continue
    }
    if (current !== null) sections.get(current).push(lines[i])
  }
  return sections
}

export function parseManifest(text) {
  const sections = splitSections(text)
  const files = new Map()
  let name = null
  for (const line of sections.get('FILES') ?? []) {
    if (line.trim() === '') continue
    if (!line.startsWith(' ')) {
      name = line.trim()
      files.set(name, {})
      continue
    }
    const field = line.trim().match(/^(bytes|sha256):\s*(\S+)$/)
    if (field && name !== null) files.get(name)[field[1]] = field[1] === 'bytes' ? Number(field[2]) : field[2]
  }

  const counts = new Map()
  for (const line of sections.get('COUNTS') ?? []) {
    const m = line.match(/^(.+):\s*(\d+)$/)
    if (m) counts.set(m[1].trim(), Number(m[2]))
  }

  const expected = (sections.get('EXPECTED ARCHIVE ROOT') ?? []).map((l) => l.trim()).filter(Boolean)
  return { files, counts, expected }
}

function measure(path) {
  const bytes = readFileSync(path)
  return { bytes: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex'), hasCr: bytes.includes(0x0d) }
}

function countMatches(text, pattern) {
  return (text.match(pattern) ?? []).length
}

export function verifyFiveFilePackage(root) {
  const errors = []
  const inventory = []
  const dir = join(root, ARCHIVE_DIR)

  if (!existsSync(dir)) return { ok: false, errors: [`${ARCHIVE_DIR}: 归档目录缺失`], inventory }

  const present = readdirSync(dir)
    .filter((n) => statSync(join(dir, n)).isFile())
    .sort()

  const manifestPath = join(dir, MANIFEST_NAME)
  const auditPath = join(dir, AUDIT_NAME)
  for (const [name, path] of [
    [MANIFEST_NAME, manifestPath],
    [AUDIT_NAME, auditPath],
  ]) {
    if (!existsSync(path)) return { ok: false, errors: [`${ARCHIVE_DIR}/${name}: 缺失,包完整性无从校验`], inventory }
  }

  const manifest = parseManifest(readFileSync(manifestPath, 'utf8'))
  let audit
  try {
    audit = JSON.parse(readFileSync(auditPath, 'utf8'))
  } catch (e) {
    return { ok: false, errors: [`${ARCHIVE_DIR}/${AUDIT_NAME}: JSON 解析失败(${e.message})`], inventory }
  }

  // ── 1. 归档文件集合与 manifest 的 EXPECTED ARCHIVE ROOT 逐名一致 ────────
  const expected = [...manifest.expected].sort()
  for (const name of present) {
    if (!expected.includes(name)) errors.push(`${ARCHIVE_DIR}/${name}: 归档内出现 manifest 未登记的文件`)
  }
  for (const name of expected) {
    if (!present.includes(name)) errors.push(`${ARCHIVE_DIR}/${name}: manifest 登记了但归档内不存在`)
  }

  // ── 2 / 3 / 6. 逐文件字节数、SHA-256 与换行 ────────────────────────────
  const actual = new Map()
  const filesWithCr = []
  for (const name of present) {
    const m = measure(join(dir, name))
    actual.set(name, m)
    inventory.push({ file: `${ARCHIVE_DIR}/${name}`, bytes: m.bytes, sha256: m.sha256 })
    if (m.hasCr) filesWithCr.push(name)
  }

  for (const [name, record] of manifest.files) {
    const real = actual.get(name)
    if (!real) continue
    if (name === MANIFEST_NAME || name === AUDIT_NAME) {
      // 自引用与循环引用:这两项在 manifest 里必须留空(见文件头校验项 2)。
      if (record.bytes !== undefined || record.sha256 !== undefined) {
        errors.push(`manifest: 记录了 ${name} 的 bytes/sha256,会构成自引用/循环引用,必须留空`)
      }
      continue
    }
    if (record.bytes === undefined || record.sha256 === undefined) {
      errors.push(`manifest: ${name} 缺 bytes 或 sha256 记录,该文件不再被守护`)
      continue
    }
    if (record.bytes !== real.bytes) errors.push(`manifest: ${name} bytes 记录 ${record.bytes},实际 ${real.bytes}`)
    if (record.sha256 !== real.sha256) errors.push(`manifest: ${name} sha256 记录 ${record.sha256},实际 ${real.sha256}`)
  }

  const auditFiles = new Map((audit.files ?? []).map((e) => [e.name, e]))
  for (const name of expected) {
    const entry = auditFiles.get(name)
    if (!entry) {
      errors.push(`audit: files[] 缺 ${name} 条目`)
      continue
    }
    const real = actual.get(name)
    if (!real) continue
    if (name === AUDIT_NAME) {
      if (entry.bytes !== null || entry.sha256 !== null) {
        errors.push('audit: 自身条目的 bytes/sha256 必须为 null(自引用约定),现被填了值')
      }
      if (!entry.note) errors.push('audit: 自身条目缺 note,自引用约定不再可读')
      continue
    }
    if (entry.bytes !== real.bytes) errors.push(`audit: ${name} bytes 记录 ${entry.bytes},实际 ${real.bytes}`)
    if (entry.sha256 !== real.sha256) errors.push(`audit: ${name} sha256 记录 ${entry.sha256},实际 ${real.sha256}`)
    const fromManifest = manifest.files.get(name)
    if (fromManifest?.sha256 !== undefined && fromManifest.sha256 !== entry.sha256) {
      errors.push(`${name}: manifest 与 audit 记录的 sha256 互相矛盾(${fromManifest.sha256} vs ${entry.sha256})`)
    }
  }

  // ── 4. 计数三方一致 ───────────────────────────────────────────────────
  const read = (name) => (actual.has(name) ? readFileSync(join(dir, name), 'utf8') : '')
  const recomputed = {
    design_modules: countMatches(read(DESIGN_NAME), /^## 12\.\d+ `[a-z-]+`$/gm),
    foundation_tasks: countMatches(read(FOUNDATION_NAME), /^### Task /gm),
    vertical_slice_tasks: countMatches(read(VERTICAL_NAME), /^### Task /gm),
    production_source_files: present.filter((n) => !ALLOWED_EXTENSIONS.has(extname(n))).length,
  }
  for (const key of COUNT_KEYS) {
    const fromManifest = manifest.counts.get(key.manifest)
    const fromAudit = audit.counts?.[key.audit]
    const real = recomputed[key.audit]
    if (fromManifest === undefined) errors.push(`manifest: COUNTS 段缺「${key.manifest}」`)
    else if (fromManifest !== real) errors.push(`manifest: ${key.label} 记录 ${fromManifest},实际 ${real}`)
    if (fromAudit === undefined) errors.push(`audit: counts 缺「${key.audit}」`)
    else if (fromAudit !== real) errors.push(`audit: ${key.label} 记录 ${fromAudit},实际 ${real}`)
  }
  if (audit.counts?.archive_files_expected !== present.length) {
    errors.push(`audit: archive_files_expected 记录 ${audit.counts?.archive_files_expected},实际 ${present.length}`)
  }
  if (expected.length !== present.length) {
    errors.push(`manifest: EXPECTED ARCHIVE ROOT 列了 ${expected.length} 个名字,归档内实际 ${present.length} 个文件`)
  }

  // ── 6. 换行归属:LF 在仓库侧,CRLF 是 Workflow 附件那一侧 ────────────────
  for (const name of filesWithCr) errors.push(`${ARCHIVE_DIR}/${name}: 含 CR 字节,仓库侧必须是 LF`)
  const lineEndingCheck = (audit.checks ?? []).find((c) => c.id === LINE_ENDING_CHECK_ID)
  if (!lineEndingCheck) {
    errors.push(`audit: checks 缺 ${LINE_ENDING_CHECK_ID},换行归属不再被记录`)
  } else {
    const recorded = [...(lineEndingCheck.details?.files_with_cr ?? [])].sort()
    if (JSON.stringify(recorded) !== JSON.stringify([...filesWithCr].sort())) {
      errors.push(`audit: ${LINE_ENDING_CHECK_ID} 记录 files_with_cr=[${recorded}],实际 [${filesWithCr}]`)
    }
    const expectedStatus = filesWithCr.length === 0 ? 'pass' : 'fail'
    if (lineEndingCheck.status !== expectedStatus) {
      errors.push(`audit: ${LINE_ENDING_CHECK_ID} status 记录「${lineEndingCheck.status}」,实际应为「${expectedStatus}」`)
    }
  }

  return { ok: errors.length === 0, errors, inventory }
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))) {
  const root = process.argv[2] ? resolve(process.argv[2]) : resolve(dirname(fileURLToPath(import.meta.url)), '..')
  const result = verifyFiveFilePackage(root)
  console.log('五文件包实测值:')
  for (const item of result.inventory) console.log(`  - ${item.file}  ${item.bytes} bytes  ${item.sha256}`)
  if (!result.ok) {
    console.error(`\nverify-five-file-package: ${result.errors.length} 处不一致\n`)
    for (const e of result.errors) console.error(`  ✗ ${e}`)
    process.exit(1)
  }
  console.log('\nverify-five-file-package: OK')
}
