import { createHash, randomBytes } from 'node:crypto'
import type { H3Event } from 'h3'

const verifierCookie = 'pc_ms_verifier'
const stateCookie = 'pc_ms_state'
const nonceCookie = 'pc_ms_nonce'

export function pkceVerifier(): string {
  return randomBytes(32).toString('base64url')
}

export function pkceChallenge(verifier: string): string {
  return createHash('sha256').update(verifier).digest('base64url')
}

export function oauthState(): string {
  return randomBytes(16).toString('base64url')
}

export function oauthCookieOptions(event: H3Event) {
  const secure = process.env.NODE_ENV === 'production' || getRequestURL(event).protocol === 'https:'
  return {
    httpOnly: true,
    sameSite: 'lax' as const,
    secure,
    path: '/',
    maxAge: 600
  }
}

export function setMicrosoftOAuthCookies(
  event: H3Event,
  state: string,
  nonce: string,
  verifier: string
) {
  const opts = oauthCookieOptions(event)
  setCookie(event, stateCookie, state, opts)
  setCookie(event, nonceCookie, nonce, opts)
  setCookie(event, verifierCookie, verifier, opts)
}

export function readMicrosoftOAuthCookies(event: H3Event) {
  return {
    state: getCookie(event, stateCookie) || '',
    nonce: getCookie(event, nonceCookie) || '',
    verifier: getCookie(event, verifierCookie) || ''
  }
}

export function clearMicrosoftOAuthCookies(event: H3Event) {
  const opts = { path: '/', maxAge: 0 }
  deleteCookie(event, stateCookie, opts)
  deleteCookie(event, nonceCookie, opts)
  deleteCookie(event, verifierCookie, opts)
}

export function microsoftAuthorizeUrl(
  tenantId: string,
  clientId: string,
  redirectUri: string,
  state: string,
  nonce: string,
  codeChallenge: string
) {
  const query = new URLSearchParams({
    client_id: clientId,
    response_type: 'code',
    redirect_uri: redirectUri,
    response_mode: 'query',
    scope: 'openid profile email',
    state,
    nonce,
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    prompt: 'select_account'
  })
  return `https://login.microsoftonline.com/${tenantId}/oauth2/v2.0/authorize?${query.toString()}`
}

export function microsoftAuthorizeUrlConfidential(
  tenantId: string,
  clientId: string,
  redirectUri: string,
  state: string,
  nonce: string
) {
  const query = new URLSearchParams({
    client_id: clientId,
    response_type: 'code',
    redirect_uri: redirectUri,
    response_mode: 'query',
    scope: 'openid profile email',
    state,
    nonce,
    prompt: 'select_account'
  })
  return `https://login.microsoftonline.com/${tenantId}/oauth2/v2.0/authorize?${query.toString()}`
}

export async function redeemMicrosoftAuthorizationCode(
  tenantId: string,
  clientId: string,
  redirectUri: string,
  code: string,
  codeVerifier: string
) {
  const body = new URLSearchParams({
    client_id: clientId,
    grant_type: 'authorization_code',
    code,
    redirect_uri: redirectUri,
    code_verifier: codeVerifier
  })
  const response = await fetch(`https://login.microsoftonline.com/${tenantId}/oauth2/v2.0/token`, {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body
  })
  const payload = (await response.json().catch(() => ({}))) as {
    id_token?: string
    error?: string
    error_description?: string
  }
  if (!response.ok || !payload.id_token) {
    const detail = payload.error_description || payload.error || 'token exchange failed'
    throw new Error(detail)
  }
  return payload.id_token
}

export async function redeemMicrosoftAuthorizationCodeConfidential(
  tenantId: string,
  clientId: string,
  clientSecret: string,
  redirectUri: string,
  code: string
) {
  const body = new URLSearchParams({
    client_id: clientId,
    client_secret: clientSecret,
    grant_type: 'authorization_code',
    code,
    redirect_uri: redirectUri,
    scope: 'openid profile email'
  })
  const response = await fetch(`https://login.microsoftonline.com/${tenantId}/oauth2/v2.0/token`, {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body
  })
  const payload = (await response.json().catch(() => ({}))) as {
    id_token?: string
    error?: string
    error_description?: string
  }
  if (!response.ok || !payload.id_token) {
    const detail = payload.error_description || payload.error || 'token exchange failed'
    throw new Error(detail)
  }
  return payload.id_token
}

export function briefOAuthError(message: string) {
  const trimmed = message.trim()
  if (trimmed.length <= 160) {
    return trimmed
  }

  const code = trimmed.match(/AADSTS\d+/)?.[0]
  return code ? `${code} — check Entra redirect URI (Web + SPA) and client ID.` : 'microsoft-sign-in'
}