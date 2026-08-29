// verify-five-file-package 自测:在临时目录搭一个五文件包,逐个制造失真形态并断言被抓、真实仓库全绿。
// 每条用例都是一次反向证明——没有「造出违规 → 变红」的证据,就不能声称这道闸门带电。
// 运行:node --test eng/verify-five-file-package.test.mjs
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, rmSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { join, dirname, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'

import {
  verifyFiveFilePackage,
  ARCHIVE_DIR,
  DESIGN_NAME,
  FOUNDATION_NAME,
  VERTICAL_NAME,
  MANIFEST_NAME,
  AUDIT_NAME,
  LINE_ENDING_CHECK_ID,
} from './verify-five-file-package.mjs'

const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')

const MODULES = ['session', 'connection', 'handshake']
const FOUNDATION_TASKS = 4
const VERTICAL_TASKS = 2

const sha = (text) => createHash('sha256').update(Buffer.from(text, 'utf8')).digest('hex')
const size = (text) => Buffer.byteLength(text, 'utf8')

function designText(extra = '') {
  return `# design\n\n${MODULES.map((m, i) => `## 12.${i + 1} \`${m}\`\n\ncontent\n`).join('\n')}${extra}`
}
const foundationText = `# foundation\n\n${Array.from({ length: FOUNDATION_TASKS }, (_, i) => `### Task F-${i}\n\nbody\n`).join('\n')}`
const verticalText = `# vertical\n\n${Array.from({ length: VERTICAL_TASKS }, (_, i) => `### Task V-${i}\n\nbody\n`).join('\n')}`

function manifestText(files, counts, expected) {
  const entries = expected
    .map((name) => {
      const record = files[name]
      const head = `${name}\n  purpose: test fixture\n`
      return record === null ? head : `${head}  bytes: ${record.bytes}\n  sha256: ${record.sha256}\n`
    })
    .join('')
  return [
    'LumioClient framework scaffolding — test fixture manifest',
    '',
    'FILES',
    '-----',
    entries.trimEnd(),
    '',
    'COUNTS',
    '------',
    `design modules: ${counts.design_modules}`,
    `foundation plan tasks: ${counts.foundation_tasks}`,
    `vertical plan tasks: ${counts.vertical_slice_tasks}`,
    `production source files in archive: ${counts.production_source_files}`,
    '',
    'EXPECTED ARCHIVE ROOT',
    '---------------------',
    ...expected,
    '',
  ].join('\n')
}

/**
 * 搭一个默认自洽的五文件包;mutate 在写盘前改动 shape,用来制造各种失真形态。
 * shape.contents 是实际写盘的内容,shape.records 是 manifest/audit 记录的值——
 * 二者默认由同一份内容算出,分开可控正是为了造「记录与实际不符」。
 */
function fixture(mutate = () => {}) {
  const root = mkdtempSync(join(tmpdir(), 'verify-five-file-'))
  const dir = join(root, ARCHIVE_DIR)
  mkdirSync(dir, { recursive: true })

  const contents = {
    [DESIGN_NAME]: designText(),
    [FOUNDATION_NAME]: foundationText,
    [VERTICAL_NAME]: verticalText,
  }
  const shape = {
    contents,
    extraFiles: {},
    expected: [DESIGN_NAME, FOUNDATION_NAME, VERTICAL_NAME, MANIFEST_NAME, AUDIT_NAME],
    counts: {
      design_modules: MODULES.length,
      foundation_tasks: FOUNDATION_TASKS,
      vertical_slice_tasks: VERTICAL_TASKS,
      production_source_files: 0,
      archive_files_expected: 5,
    },
    manifestOverrides: {},
    auditOverrides: {},
    auditSelfEntry: { name: AUDIT_NAME, bytes: null, sha256: null, note: 'Self-referential checksum omitted inside this file.' },
    lineEndingCheck: { id: LINE_ENDING_CHECK_ID, status: 'pass', details: { files_with_cr: [] } },
    dropLineEndingCheck: false,
  }
  mutate(shape)

  for (const [name, text] of Object.entries(shape.contents)) writeFileSync(join(dir, name), text)
  for (const [name, text] of Object.entries(shape.extraFiles)) writeFileSync(join(dir, name), text)

  const records = {}
  for (const name of [DESIGN_NAME, FOUNDATION_NAME, VERTICAL_NAME]) {
    const text = shape.contents[name]
    records[name] = text === undefined ? null : { bytes: size(text), sha256: sha(text) }
  }
  Object.assign(records, shape.manifestOverrides)
  records[MANIFEST_NAME] = null
  records[AUDIT_NAME] = null

  const manifest = manifestText(records, shape.counts, shape.expected)
  writeFileSync(join(dir, MANIFEST_NAME), manifest)

  const auditFiles = shape.expected
    .filter((n) => n !== AUDIT_NAME)
    .map((name) => {
      if (name === MANIFEST_NAME) return { name, bytes: size(manifest), sha256: sha(manifest) }
      const override = shape.auditOverrides[name]
      if (override) return { name, ...override }
      const text = shape.contents[name]
      // 文件被删掉时记录仍在(正是要证明「登记了但不存在」会被抓)。
      return text === undefined ? { name, bytes: 0, sha256: sha('') } : { name, bytes: size(text), sha256: sha(text) }
    })
  auditFiles.push(shape.auditSelfEntry)

  const audit = {
    schema_version: 1,
    counts: shape.counts,
    files: auditFiles,
    checks: shape.dropLineEndingCheck ? [] : [shape.lineEndingCheck],
    overall_status: 'pass',
  }
  writeFileSync(join(dir, AUDIT_NAME), JSON.stringify(audit, null, 2) + '\n')
  return root
}

function withFixture(mutate, assertions) {
  const root = fixture(mutate)
  try {
    assertions(verifyFiveFilePackage(root), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const hasError = (result, needle) => result.errors.some((e) => e.includes(needle))

test('真实仓库全绿', () => {
  const result = verifyFiveFilePackage(REPO_ROOT)
  assert.deepEqual(result.errors, [])
  assert.equal(result.inventory.length, 5)
})

test('自洽的临时包全绿(确认 fixture 本身不是永远红)', () => {
  withFixture(() => {}, (result) => assert.deepEqual(result.errors, []))
})

test('被记录文件改一个字节而记录不动被抓(T-00006 事故形态)', () => {
  withFixture(
    (shape) => {
      const original = shape.contents[DESIGN_NAME]
      shape.manifestOverrides[DESIGN_NAME] = { bytes: size(original), sha256: sha(original) }
      shape.auditOverrides[DESIGN_NAME] = { bytes: size(original), sha256: sha(original) }
      shape.contents[DESIGN_NAME] = designText('\n<!-- one more byte -->\n')
    },
    (result) => {
      assert.ok(hasError(result, `manifest: ${DESIGN_NAME} sha256 记录`), result.errors.join(' | '))
      assert.ok(hasError(result, `audit: ${DESIGN_NAME} sha256 记录`), result.errors.join(' | '))
      assert.ok(hasError(result, `manifest: ${DESIGN_NAME} bytes 记录`), result.errors.join(' | '))
    },
  )
})

test('manifest 与 audit 记录互相矛盾被抓', () => {
  withFixture(
    (shape) => {
      shape.auditOverrides[FOUNDATION_NAME] = { bytes: size(shape.contents[FOUNDATION_NAME]), sha256: sha('other') }
    },
    (result) => assert.ok(hasError(result, '互相矛盾'), result.errors.join(' | ')),
  )
})

test('归档多出未登记文件被抓,且计入 production source files', () => {
  withFixture(
    (shape) => {
      shape.extraFiles['Leaked.cs'] = 'class Leaked {}\n'
    },
    (result) => {
      assert.ok(hasError(result, '归档内出现 manifest 未登记的文件'), result.errors.join(' | '))
      assert.ok(hasError(result, 'production source files 记录 0,实际 1'), result.errors.join(' | '))
    },
  )
})

test('登记的文件从归档消失被抓', () => {
  withFixture(
    (shape) => {
      delete shape.contents[VERTICAL_NAME]
    },
    (result) => assert.ok(hasError(result, `${VERTICAL_NAME}: manifest 登记了但归档内不存在`), result.errors.join(' | ')),
  )
})

test('计数漂移被抓(manifest 与 audit 两侧都报)', () => {
  withFixture(
    (shape) => {
      shape.counts = { ...shape.counts, foundation_tasks: FOUNDATION_TASKS + 1 }
    },
    (result) => {
      assert.ok(hasError(result, 'manifest: foundation plan tasks 记录'), result.errors.join(' | '))
      assert.ok(hasError(result, 'audit: foundation plan tasks 记录'), result.errors.join(' | '))
    },
  )
})

test('仓库侧混入 CRLF 被抓,且 audit 的换行结论随之不自洽', () => {
  withFixture(
    (shape) => {
      shape.contents[FOUNDATION_NAME] = foundationText.replaceAll('\n', '\r\n')
    },
    (result) => {
      assert.ok(hasError(result, `${FOUNDATION_NAME}: 含 CR 字节`), result.errors.join(' | '))
      assert.ok(hasError(result, `${LINE_ENDING_CHECK_ID} 记录 files_with_cr=[]`), result.errors.join(' | '))
    },
  )
})

test('audit 自哈希被填值被抓(自引用约定失效)', () => {
  withFixture(
    (shape) => {
      shape.auditSelfEntry = { name: AUDIT_NAME, bytes: 1, sha256: sha('anything'), note: 'x' }
    },
    (result) => assert.ok(hasError(result, '自身条目的 bytes/sha256 必须为 null'), result.errors.join(' | ')),
  )
})

test('manifest 记录 audit 哈希被抓(会构成循环引用)', () => {
  const root = fixture()
  try {
    const p = join(root, ARCHIVE_DIR, MANIFEST_NAME)
    const text = readFileSync(p, 'utf8').replace(
      `${AUDIT_NAME}\n  purpose: test fixture\n`,
      `${AUDIT_NAME}\n  purpose: test fixture\n  bytes: 1\n  sha256: ${sha('anything')}\n`,
    )
    writeFileSync(p, text)
    const result = verifyFiveFilePackage(root)
    assert.ok(hasError(result, '会构成自引用/循环引用'), result.errors.join(' | '))
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('audit 丢掉换行检查被抓(守护静默消失)', () => {
  withFixture(
    (shape) => {
      shape.dropLineEndingCheck = true
    },
    (result) => assert.ok(hasError(result, `checks 缺 ${LINE_ENDING_CHECK_ID}`), result.errors.join(' | ')),
  )
})
