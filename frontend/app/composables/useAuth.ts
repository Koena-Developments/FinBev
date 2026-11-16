import type { MeResponse } from '~/composables/useMe'

const TOKEN_KEY = 'pettycash_token'

let sessionProbe: Promise<boolean> | null = null

export function useAuth() {
  /** Presence only. The JWT stays in an HttpOnly cookie and is never stored here. */
  const token = useState<string | null>('auth-token', () => null)
  const sessionReady = useState<boolean>('auth-session-ready', () => false)
  /** Bumps whenever the session changes so profile + cached pages reload for this tab. */
  const sessionId = useState<number>('auth-session-id', () => 0)

  function clearLegacyToken() {
    if (!import.meta.client) return
    localStorage.removeItem(TOKEN_KEY)
    sessionStorage.removeItem(TOKEN_KEY)
  }

  async function ensureSession() {
    if (!import.meta.client) return false
    clearLegacyToken()
    if (sessionReady.value) return !!token.value
    if (!sessionProbe) {
      sessionProbe = (async () => {
        try {
          await useApi()<MeResponse>('/api/auth/me')
          token.value = 'cookie'
          return true
        } catch {
          token.value = null
          return false
        } finally {
          sessionReady.value = true
          sessionProbe = null
        }
      })()
    }
    return sessionProbe
  }

  function markSignedIn() {
    clearLegacyToken()
    token.value = 'cookie'
    sessionReady.value = true
    sessionId.value++
    useMe().resetMe()
    if (import.meta.client) clearNuxtData()
  }

  async function signOut() {
    token.value = null
    sessionReady.value = true
    sessionId.value++
    clearLegacyToken()
    useMe().resetMe()
    if (import.meta.client) {
      clearNuxtData()
      try {
        await fetch('/api/auth/logout', {
          method: 'POST',
          credentials: 'include',
          headers: { 'X-Requested-With': 'PettyCash' }
        })
      } catch {
        // The local session is already cleared.
      }
      if (useRoute().path !== '/login') await navigateTo('/login')
    }
  }

  async function fetchMe() {
    return useApi()<MeResponse>('/api/auth/me')
  }

  function hasRole(role: string, roles: string[]) {
    return roles.includes(role)
  }

  return { token, sessionId, ensureSession, markSignedIn, signOut, fetchMe, hasRole }
}
