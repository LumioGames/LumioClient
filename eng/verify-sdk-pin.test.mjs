// verify-sdk-pin 自测:在临时目录搭出各个 SDK pin 出现点,断言各类漂移被抓、真实仓库全绿。
// 运行:node --test eng/verify-sdk-pin.test.mjs
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { join, dirname, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'

import { verifySdkPin, PIN_COPIES, WORKFLOW_DIR } from './verify-sdk-pin.mjs'

const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const INSTALLED = () => ['10.0.400']
const GOOD_GLOBAL = JSON.stringify({ sdk: { version: '10.0.400', rollForward: 'disable', allowPrerelease: false } })

/** 搭一个只含 pin 出现点的临时仓库;pin 为副本里写入的字面量。 */
function fixture({ global: globalJson = GOOD_GLOBAL, pin = '10.0.400', workflow = null } = {}) {
  const root = mkdtempSync(join(tmpdir(), 'verify-sdk-pin-'))
  writeFileSync(join(root, 'global.json'), globalJson)
  for (const copy of PIN_COPIES) {
    const p = join(root, copy.file)
    mkdirSync(dirname(p), { recursive: true })
    writeFileSync(p, copy.sample.replaceAll('__PIN__', pin))
  }
  if (workflow !== null) {
    const p = join(root, WORKFLOW_DIR, 'repository-policy.yml')
    mkdirSync(dirname(p), { recursive: true })
    writeFileSync(p, workflow)
  }
  return root
}

function withFixture(options, assertions) {
  const root = fixture(options)
  try {
    assertions(root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

test('真实仓库全绿', () => {
  const result = verifySdkPin(REPO_ROOT, { installedSdks: INSTALLED })
  assert.deepEqual(result.errors, [])
  assert.equal(result.pinnedVersion, '10.0.400')
})

test('运行时补丁号形态的假 SDK 版本被抓(LumioGameRuntime 事故形态)', () => {
  withFixture({ global: JSON.stringify({ sdk: { version: '10.0.11', rollForward: 'disable', allowPrerelease: false } }), pin: '10.0.11' }, (root) => {
    const errors = verifySdkPin(root, { installedSdks: INSTALLED }).errors
    assert.ok(errors.some((e) => e.includes('10.0.11') && e.includes('功能带')), errors.join(' | '))
  })
})

test('形态合法但本机未安装时报可读错误', () => {
  withFixture({}, (root) => {
    const errors = verifySdkPin(root, { installedSdks: () => ['9.0.100', '10.0.100'] }).errors
    assert.ok(errors.some((e) => e.includes('未安装') && e.includes('10.0.400')), errors.join(' | '))
  })
})

test('取不到已装 SDK 列表时降级为跳过,不误报', () => {
  withFixture({}, (root) => {
    const result = verifySdkPin(root, { installedSdks: () => null })
    assert.deepEqual(result.errors, [])
    assert.ok(result.skipped.some((s) => s.includes('已安装')), result.skipped.join(' | '))
  })
})

test('副本出现点与 global.json 漂移被抓', () => {
  withFixture({ pin: '10.0.401' }, (root) => {
    const errors = verifySdkPin(root, { installedSdks: INSTALLED }).errors
    for (const copy of PIN_COPIES) {
      assert.ok(errors.some((e) => e.includes(copy.file)), `${copy.file} 未被抓:${errors.join(' | ')}`)
    }
  })
})

test('副本里的 pin 整个消失也被抓(而不是静默放行)', () => {
  withFixture({}, (root) => {
    writeFileSync(join(root, PIN_COPIES[0].file), '# 这里再也没有 SDK pin 了\n')
    const errors = verifySdkPin(root, { installedSdks: INSTALLED }).errors
    assert.ok(errors.some((e) => e.includes(PIN_COPIES[0].file) && e.includes('未找到')), errors.join(' | '))
  })
})

test('rollForward 与 allowPrerelease 的既有约束仍被校验', () => {
  withFixture({ global: JSON.stringify({ sdk: { version: '10.0.400', rollForward: 'latestMinor', allowPrerelease: true } }) }, (root) => {
    const errors = verifySdkPin(root, { installedSdks: INSTALLED }).errors
    assert.ok(errors.some((e) => e.includes('rollForward')), errors.join(' | '))
    assert.ok(errors.some((e) => e.includes('allowPrerelease')), errors.join(' | '))
  })
})

test('CI 引入 dotnet 版本固定时同一校验覆盖之', () => {
  const workflow = ['jobs:', '  readme:', '    steps:', '      - uses: actions/setup-dotnet@v4', '        with:', '          dotnet-version: 10.0.399', ''].join('\n')
  withFixture({ workflow }, (root) => {
    const errors = verifySdkPin(root, { installedSdks: INSTALLED }).errors
    assert.ok(errors.some((e) => e.includes('dotnet-version') && e.includes('10.0.399')), errors.join(' | '))
  })
})

test('CI dotnet 版本与 global.json 一致时放行', () => {
  const workflow = ['jobs:', '  readme:', '    steps:', '      - uses: actions/setup-dotnet@v4', '        with:', '          dotnet-version: 10.0.400', ''].join('\n')
  withFixture({ workflow }, (root) => {
    assert.deepEqual(verifySdkPin(root, { installedSdks: INSTALLED }).errors, [])
  })
})

test('清单列出全部不被 restore/lock 覆盖的版本号出现点', () => {
  const listed = verifySdkPin(REPO_ROOT, { installedSdks: INSTALLED }).inventory.map((i) => i.file)
  assert.ok(listed.includes('global.json'))
  assert.ok(listed.includes(`${WORKFLOW_DIR}/repository-policy.yml`))
  for (const copy of PIN_COPIES) assert.ok(listed.includes(copy.file), `清单缺 ${copy.file}`)
})
