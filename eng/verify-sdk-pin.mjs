#!/usr/bin/env node
/**
 * verify-sdk-pin — 给「不被 restore / lock 覆盖的版本号出现点」补机器校验。
 * 用法:node eng/verify-sdk-pin.mjs [仓库根目录]   (省略参数时取本脚本上级目录)
 *
 * 为什么不是一个 dotnet 测试:global.json 写坏时 dotnet 自身就起不来
 * ("A compatible .NET SDK was not found",exit 155),任何 dotnet test 都跑不到。
 * 所以这道闸门必须由不依赖 .NET 的运行时(node)承担,才能给出可读错误。
 *
 * 校验项(本注释是该脚本能力清单的单一权威):
 *  1. global.json 的 sdk.version 必须是 .NET SDK 的功能带形态 `<major>.<minor>.<3 位>`。
 *     运行时补丁号形态(如 10.0.11)不是合法 SDK 版本——LumioGameRuntime 正是在此处锁死整条工具链。
 *  2. rollForward 必须为 disable、allowPrerelease 必须为 false(既有工具链纪律)。
 *  3. pin 的每处副本必须与 global.json 一致,且不得整个消失(消失 = 校验静默失效)。
 *  4. CI workflow 里的 dotnet-version 若存在,必须与 global.json 一致。
 *  5. 该 SDK 版本必须在本机已安装;取不到已装列表时降级为跳过,不误报(离线可构建性优先,不联网)。
 */
import { readFileSync, existsSync, readdirSync } from 'node:fs'
import { join, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { execFileSync } from 'node:child_process'

export const WORKFLOW_DIR = '.github/workflows'

/** SDK pin 在 global.json 之外的副本:每处给出取值正则与自测用样本。 */
export const PIN_COPIES = [
  {
    file: 'eng/verify-toolchain.sh',
    pattern: /grep -q '([\d.]+)' global\.json/,
    sample: "#!/usr/bin/env bash\ndotnet --info\ngrep -q '__PIN__' global.json\n",
  },
  {
    file: 'eng/verify-toolchain.ps1',
    pattern: /"version":\\s\*"([\d.]+)"/,
    sample: '$ErrorActionPreference = "Stop"\nif ((Get-Content "./global.json" -Raw) -notmatch \'"version":\\s*"__PIN__"\') { throw "SDK pin missing" }\n',
  },
  {
    file: 'tests/Lumio.Client.ArchitectureTests/Toolchain/ToolchainPolicyTests.cs',
    pattern: /Assert\.Equal\("([\d.]+)", sdk\.GetProperty\("version"\)/,
    sample: 'public sealed class ToolchainPolicyTests\n{\n    Assert.Equal("__PIN__", sdk.GetProperty("version").GetString());\n}\n',
  },
]

const FEATURE_BAND = /^\d+\.\d+\.\d{3}$/

/** 读本机已安装的 SDK 版本号;dotnet 不可用时返回 null(交由调用方降级)。 */
function readInstalledSdks() {
  try {
    return execFileSync('dotnet', ['--list-sdks'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] })
      .split('\n')
      .map((line) => line.trim().split(' ')[0])
      .filter(Boolean)
  } catch {
    return null
  }
}

export function verifySdkPin(root, { installedSdks = readInstalledSdks } = {}) {
  const errors = []
  const skipped = []
  const inventory = []

  const globalPath = join(root, 'global.json')
  if (!existsSync(globalPath)) {
    return { ok: false, pinnedVersion: null, errors: ['global.json: 缺失'], skipped, inventory }
  }
  const sdk = JSON.parse(readFileSync(globalPath, 'utf8')).sdk ?? {}
  const pinned = sdk.version ?? null
  inventory.push({ file: 'global.json', field: 'sdk.version', value: pinned })

  // ── 1 / 2. global.json 自身 ──────────────────────────────────────────
  if (!pinned) {
    errors.push('global.json: 缺少 sdk.version')
  } else if (!FEATURE_BAND.test(pinned)) {
    errors.push(
      `global.json: sdk.version「${pinned}」不是 .NET SDK 的功能带形态(<major>.<minor>.<3 位>,如 10.0.400)` +
        '——运行时补丁号不是 SDK 版本,锁成它会让整条 dotnet 工具链不可用',
    )
  }
  if (sdk.rollForward !== 'disable') errors.push(`global.json: rollForward 必须为 disable,实际「${sdk.rollForward ?? ''}」`)
  if (sdk.allowPrerelease !== false) errors.push(`global.json: allowPrerelease 必须为 false,实际「${sdk.allowPrerelease ?? ''}」`)

  // ── 3. pin 的各处副本必须与 global.json 一致 ─────────────────────────
  for (const copy of PIN_COPIES) {
    const p = join(root, copy.file)
    if (!existsSync(p)) {
      errors.push(`${copy.file}: 缺失,SDK pin 副本校验无法执行`)
      continue
    }
    const found = readFileSync(p, 'utf8').match(copy.pattern)?.[1] ?? null
    inventory.push({ file: copy.file, field: 'SDK pin 副本', value: found })
    if (found === null) errors.push(`${copy.file}: 未找到 SDK pin 字面量,校验已静默失效`)
    else if (found !== pinned) errors.push(`${copy.file}: SDK pin「${found}」与 global.json「${pinned}」漂移`)
  }

  // ── 4. CI 里的 dotnet 版本固定 ───────────────────────────────────────
  const workflowDir = join(root, WORKFLOW_DIR)
  if (existsSync(workflowDir)) {
    for (const name of readdirSync(workflowDir)) {
      if (!name.endsWith('.yml') && !name.endsWith('.yaml')) continue
      const rel = `${WORKFLOW_DIR}/${name}`
      const text = readFileSync(join(workflowDir, name), 'utf8')
      const versions = [...text.matchAll(/dotnet-version:\s*['"]?([\d.x*]+)['"]?/g)].map((m) => m[1])
      inventory.push({ file: rel, field: 'dotnet-version', value: versions.length ? versions.join(', ') : '(未固定)' })
      for (const version of versions) {
        if (version !== pinned) errors.push(`${rel}: dotnet-version「${version}」与 global.json「${pinned}」不一致`)
      }
    }
  }

  // ── 5. 本机已安装(离线;取不到即跳过) ────────────────────────────────
  const installed = installedSdks()
  if (installed === null) {
    skipped.push('本机已安装 SDK 校验:dotnet 不可用,已跳过')
  } else if (pinned && FEATURE_BAND.test(pinned) && !installed.includes(pinned)) {
    errors.push(`global.json: sdk.version「${pinned}」在本机未安装(dotnet --list-sdks 实得:${installed.join(', ') || '空'})`)
  }

  return { ok: errors.length === 0, pinnedVersion: pinned, errors, skipped, inventory }
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))) {
  const root = process.argv[2] ? resolve(process.argv[2]) : resolve(dirname(fileURLToPath(import.meta.url)), '..')
  const result = verifySdkPin(root)
  console.log('不被 restore / lock 覆盖的版本号出现点:')
  for (const item of result.inventory) console.log(`  - ${item.file} [${item.field}] = ${item.value ?? '(未找到)'}`)
  for (const s of result.skipped) console.log(`  ! ${s}`)
  if (!result.ok) {
    console.error(`\nverify-sdk-pin: ${result.errors.length} 处不一致\n`)
    for (const e of result.errors) console.error(`  ✗ ${e}`)
    process.exit(1)
  }
  console.log('\nverify-sdk-pin: OK')
}
