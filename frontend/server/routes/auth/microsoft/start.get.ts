import {
  microsoftAuthorizeUrl,
  oauthState,
  pkceChallenge,
  pkceVerifier,
  setMicrosoftOAuthCookies
} from '../../../utils/microsoftAuth'

export default defineEventHandler((event) => {
  const config = useRuntimeConfig()
  const tenantId = String(config.microsoftTenantId || '').trim()
  const clientId = String(config.microsoftClientId || '').trim()
  if (!tenantId || !clientId) {
    return sendRedirect(event, '/login?error=microsoft-not-configured', 302)
  }

  const state = oauthState()
  const nonce = oauthState()
  const verifier = pkceVerifier()
  setMicrosoftOAuthCookies(event, state, nonce, verifier)

  const redirectUri = `${getRequestURL(event).origin}/auth/microsoft/callback`
  const url = microsoftAuthorizeUrl(
    tenantId,
    clientId,
    redirectUri,
    state,
    nonce,
    pkceChallenge(verifier)
  )
  return sendRedirect(event, url, 302)
})