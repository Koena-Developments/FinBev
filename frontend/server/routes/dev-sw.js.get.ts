/** Stale browser SW registrations often request this path; respond so dev logs stay clean. */
export default defineEventHandler((event) => {
  setHeader(event, 'Content-Type', 'application/javascript; charset=utf-8')
  setHeader(event, 'Cache-Control', 'no-store')
  return '// Petty Cash: no service worker in dev\n'
})
