export function clientRequestHeaders(extra?: HeadersInit) {
  const headers = new Headers(extra)
  headers.set('X-Requested-With', 'PettyCash')
  return headers
}

export function useApi() {
  const { token } = useAuth()

  return async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
    const headers = clientRequestHeaders(options.headers)
    if (!headers.has('Content-Type') && options.body) {
      headers.set('Content-Type', 'application/json')
    }

    const response = await fetch(path, {
      ...options,
      headers,
      credentials: 'include'
    })

    if (!response.ok) {
      const body = (await response.json().catch(() => ({}))) as { message?: string }
      const isPublicAuth =
        path.endsWith('/api/auth/login') ||
        path.endsWith('/api/auth/change-password') ||
        path.endsWith('/api/auth/reset-password') ||
        path.endsWith('/api/auth/logout')
      const sessionProbe = path.endsWith('/api/auth/me')
      if (response.status === 401 && import.meta.client && !isPublicAuth && !sessionProbe && token.value) {
        await useAuth().signOut()
      }
      throw new Error(body.message || failureMessage(response.status))
    }

    if (response.status === 204) {
      return undefined as T
    }

    return response.json() as Promise<T>
  }
}

function failureMessage(status: number) {
  if (status === 401) return 'Your session has ended. Sign in again.'
  if (status === 403) return 'You do not have permission to do that.'
  if (status === 404) return 'We could not find that.'
  if (status === 413) return 'That file is too large. Each file must be 10 MB or smaller.'
  if (status === 429) return 'Too many attempts. Wait a minute and try again.'
  return 'We could not complete that action. Refresh the page and try again.'
}
