import {
  briefOAuthError,
  clearMicrosoftOAuthCookies,
  readMicrosoftOAuthCookies,
  redeemMicrosoftAuthorizationCode,
  redeemMicrosoftAuthorizationCodeConfidential
} from '../../../utils/microsoftAuth'

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const tenantId = String(process.env.NUXT_MICROSOFT_TENANT_ID || '').trim()
  const clientId = String(process.env.NUXT_MICROSOFT_CLIENT_ID || '').trim()
  const clientSecret = String(process.env.NUXT_MICROSOFT_CLIENT_SECRET || '').trim()
  const confidential = clientSecret.length > 0
  const query = getQuery(event)
  const error = typeof query.error === 'string' ? query.error : ''
  if (error) {
    clearMicrosoftOAuthCookies(event)
    return sendRedirect(event, `/login?error=${encodeURIComponent(error)}`, 302)
  }

  const code = typeof query.code === 'string' ? query.code : ''
  const returnedState = typeof query.state === 'string' ? query.state : ''
  const { state, nonce, verifier } = readMicrosoftOAuthCookies(event)
  clearMicrosoftOAuthCookies(event)

  if (
    !tenantId
    || !clientId
    || !code
    || !returnedState
    || returnedState !== state
    || !nonce
    || (!confidential && !verifier)
  ) {
    return sendRedirect(event, '/login?error=invalid-callback', 302)
  }

  const redirectUri = `${getRequestURL(event).origin}/auth/microsoft/callback`
  let idToken: string
  try {
    idToken = confidential
      ? await redeemMicrosoftAuthorizationCodeConfidential(
          tenantId,
          clientId,
          clientSecret,
          redirectUri,
          code
        )
      : await redeemMicrosoftAuthorizationCode(tenantId, clientId, redirectUri, code, verifier)
  } catch (exchangeError) {
    const message = exchangeError instanceof Error ? exchangeError.message : 'microsoft-sign-in'
    return sendRedirect(
      event,
      `/login?error=${encodeURIComponent(briefOAuthError(message))}`,
      302
    )
  }

  const apiBase = String(process.env.NUXT_API_BASE || config.apiBase || '').replace(/\/$/, '')
  const proxySecret = String(process.env.NUXT_PROXY_SECRET || config.proxySecret || '')
  if (!apiBase) {
    return sendRedirect(event, '/login?error=api-not-configured', 302)
  }

  const headers = new Headers({
    'content-type': 'application/json',
    'x-requested-with': 'PettyCash'
  })
  if (proxySecret) {
    headers.set('x-pettycash-proxy', proxySecret)
  }
  const clientIp = getRequestIP(event, { xForwardedFor: true })
  if (clientIp) {
    headers.set('x-pettycash-client', clientIp)
  }

  let upstream: Response
  try {
    upstream = await fetch(`${apiBase}/api/auth/microsoft/session`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ idToken, nonce })
    })
  } catch {
    return sendRedirect(event, '/login?error=api-unavailable', 302)
  }

  const body = (await upstream.json().catch(() => ({}))) as { success?: boolean; message?: string }
  if (!upstream.ok || !body.success) {
    const message = body.message || 'sign-in-failed'
    return sendRedirect(event, `/login?error=${encodeURIComponent(message)}`, 302)
  }

  for (const cookie of upstream.headers.getSetCookie()) {
    appendResponseHeader(event, 'set-cookie', cookie)
  }

  return sendRedirect(event, '/', 302)
})