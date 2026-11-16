import { randomBytes } from 'node:crypto'
import { getResponseHeader, setResponseHeader } from 'h3'

const isDev = process.env.NODE_ENV === 'development'

function policy(scriptSrc: string, styleSrc: string) {
  return [
    "default-src 'self'",
    scriptSrc,
    styleSrc,
    "style-src-attr 'unsafe-inline'",
    "font-src 'self' https://fonts.gstatic.com data:",
    "img-src 'self' data: blob:",
    "connect-src 'self'",
    "frame-ancestors 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "object-src 'none'"
  ].join('; ')
}

function stamp(chunk: string, nonce: string) {
  return chunk
    .replace(/<script(?![^>]*\snonce=)/gi, `<script nonce="${nonce}"`)
    .replace(/<style(?![^>]*\snonce=)/gi, `<style nonce="${nonce}"`)
}

export default defineNitroPlugin((nitroApp) => {
  nitroApp.hooks.hook('render:html', (html, { event }) => {
    if (isDev) {
      setResponseHeader(
        event,
        'Content-Security-Policy',
        policy("script-src 'self' 'unsafe-inline' 'unsafe-eval'", "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com")
      )
      return
    }

    const nonce = randomBytes(16).toString('base64url')
    const apply = (chunks: string[]) => chunks.map((chunk) => stamp(chunk, nonce))
    html.head = apply(html.head)
    html.body = apply(html.body)
    html.bodyPrepend = apply(html.bodyPrepend)
    html.bodyAppend = apply(html.bodyAppend)
    setResponseHeader(
      event,
      'Content-Security-Policy',
      policy(`script-src 'self' 'nonce-${nonce}'`, `style-src 'self' 'nonce-${nonce}' https://fonts.googleapis.com`)
    )
  })

  nitroApp.hooks.hook('beforeResponse', (event) => {
    if (getResponseHeader(event, 'content-security-policy')) return
    const scriptSrc = isDev ? "script-src 'self' 'unsafe-inline' 'unsafe-eval'" : "script-src 'self'"
    const styleSrc = isDev
      ? "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com"
      : "style-src 'self' https://fonts.googleapis.com"
    setResponseHeader(event, 'Content-Security-Policy', policy(scriptSrc, styleSrc))
  })
})
