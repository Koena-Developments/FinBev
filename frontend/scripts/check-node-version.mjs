const version = process.version.slice(1)
const [major, minor] = version.split('.').map(Number)

const supported =
  (major === 22 && minor >= 21) ||
  (major === 24 && minor >= 11) ||
  major >= 26

if (!supported) {
  console.error('')
  console.error(`Node.js ${process.version} is not supported by Nuxt 4 for this project.`)
  console.error('Use Node ^22.21.0, ^24.11.0, or >=26.0.0.')
  console.error('')
  console.error('Node is the runtime — do not run "npm install 22.21.0" (that looks for an npm package).')
  console.error('')
  console.error('Windows without nvm:')
  console.error('  winget install OpenJS.NodeJS.22')
  console.error('Or download the 22 LTS installer: https://nodejs.org/en/download')
  console.error('')
  console.error('If you still see v25 after installing, remove the old folder from PATH, e.g.:')
  console.error('  C:\\Program Files\\node-v25.1.0-win-x64')
  console.error('Then open a new terminal and run: node -v')
  console.error('')
  process.exit(1)
}
