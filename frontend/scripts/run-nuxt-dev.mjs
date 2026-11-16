import { spawn, execFileSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'

const node22Exe = 'C:\\Program Files\\nodejs\\node.exe'

function resolveNodeExe() {
  if (process.platform === 'win32' && existsSync(node22Exe)) {
    return node22Exe
  }
  return process.execPath
}

function versionSupported(versionString) {
  const version = versionString.replace(/^v/, '')
  const [major, minor] = version.split('.').map(Number)
  return (major === 22 && minor >= 21) || (major === 24 && minor >= 11) || major >= 26
}

const nodeExe = resolveNodeExe()
const nodeVersion = execFileSync(nodeExe, ['-v'], { encoding: 'utf8' }).trim()

if (!versionSupported(nodeVersion)) {
  console.error('')
  console.error(`Node.js ${nodeVersion} is not supported by Nuxt 4 for this project.`)
  console.error('Use Node ^22.21.0, ^24.11.0, or >=26.0.0.')
  console.error('')
  console.error('Node is the runtime — do not run "npm install 22.21.0".')
  console.error('')
  console.error('Install Node 22:  powershell -ExecutionPolicy Bypass -File ..\\scripts\\install-node-22.ps1')
  console.error('Or:             winget install OpenJS.NodeJS.22')
  console.error('')
  console.error('If you still see v25, remove from Path: C:\\Program Files\\node-v25.1.0-win-x64')
  console.error('')
  process.exit(1)
}

if (nodeExe !== process.execPath) {
  console.log(`Using ${nodeExe} (${nodeVersion}) for Nuxt dev.`)
}

const nuxtBin = path.join(process.cwd(), 'node_modules', 'nuxt', 'bin', 'nuxt.mjs')
const child = spawn(nodeExe, [nuxtBin, 'dev'], {
  stdio: 'inherit',
  env: process.env,
  cwd: process.cwd()
})
child.on('exit', (code) => process.exit(code ?? 0))
