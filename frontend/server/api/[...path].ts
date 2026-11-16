import { appendResponseHeader, defineEventHandler, getCookie, getRequestHeader, getRequestIP, readRawBody, setResponseHeader, setResponseStatus } from 'h3'

const maxBodyBytes = 12 * 1024 * 1024

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const base = String(config.apiBase || '').replace(/\/$/, '')
  if (!base) {
    setResponseStatus(event, 500)
    return { message: 'API is not configured.' }
  }

  const headers = new Headers()
  const contentType = getRequestHeader(event, 'content-type')
  if (contentType) headers.set('content-type', contentType)
  const requestedWith = getRequestHeader(event, 'x-requested-with')
  if (requestedWith) headers.set('x-requested-with', requestedWith)

  const secret = String(config.proxySecret || '')
  if (process.env.NODE_ENV === 'production' && secret.length < 32) {
    console.error('NUXT_PROXY_SECRET must match Render Proxy__Secret and be at least 32 characters.')
    setResponseStatus(event, 500)
    return { message: 'Sign-in is not available yet. Contact IT.' }
  }
  if (secret) headers.set('x-pettycash-proxy', secret)

  const clientIp = getRequestIP(event, { xForwardedFor: true })
  if (clientIp) headers.set('x-pettycash-client', clientIp)

  const session = getCookie(event, 'pc_session')
  if (session) headers.set('authorization', `Bearer ${session}`)

  const method = event.method.toUpperCase()
  const init: RequestInit = { method, headers }
  if (method !== 'GET' && method !== 'HEAD') {
    const raw = await readRawBody(event, false)
    if (raw && raw.length > maxBodyBytes) {
      setResponseStatus(event, 413)
      return { message: 'Upload is too large.' }
    }
    if (raw && raw.length > 0) init.body = new Uint8Array(raw)
  }

  let upstream: Response
  try {
    upstream = await fetch(`${base}${event.path}`, init)
  } catch {
    setResponseStatus(event, 502)
    return { message: 'The service is unavailable.' }
  }

  setResponseStatus(event, upstream.status)
  copyHeader(event, upstream, 'content-type')
  copyHeader(event, upstream, 'content-disposition')
  copyHeader(event, upstream, 'cache-control')
  for (const cookie of upstream.headers.getSetCookie()) {
    appendResponseHeader(event, 'set-cookie', cookie)
  }

  return Buffer.from(await upstream.arrayBuffer())
})

function copyHeader(event: Parameters<typeof setResponseHeader>[0], upstream: Response, name: string) {
  const value = upstream.headers.get(name)
  if (value) setResponseHeader(event, name, value)
}
