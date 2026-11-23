import {
  microsoftAuthorizeUrl,
  microsoftAuthorizeUrlConfidential,
  oauthState,
  pkceChallenge,
  pkceVerifier,
  setMicrosoftOAuthCookies
} from '../../../utils/microsoftAuth'

export default defineEventHandler((event) => {
  const tenantId = String(process.env.NUXT_MICROSOFT_TENANT_ID || '').trim()
  const clientId = String(process.env.NUXT_MICROSOFT_CLIENT_ID || '').trim()
  const clientSecret = String(process.env.NUXT_MICROSOFT_CLIENT_SECRET || '').trim()
  if (!tenantId || !clientId) {
    return sendRedirect(event, '/login?error=microsoft-not-configured', 302)
  }

  const state = oauthState()
  const nonce = oauthState()
  const verifier = clientSecret ? '' : pkceVerifier()
  setMicrosoftOAuthCookies(event, state, nonce, verifier)

  const redirectUri = `${getRequestURL(event).origin}/auth/microsoft/callback`
  const url = clientSecret
    ? microsoftAuthorizeUrlConfidential(tenantId, clientId, redirectUri, state, nonce)
    : microsoftAuthorizeUrl(
        tenantId,
        clientId,
        redirectUri,
        state,
        nonce,
        pkceChallenge(verifier)
      )
  return sendRedirect(event, url, 302)
})