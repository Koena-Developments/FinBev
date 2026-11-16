export type MeResponse = {
  userId: string
  username: string
  displayName: string
  title: string
  departmentId: string
  departmentName: string
  roles: string[]
  mustChangePassword: boolean
  /** API uses delegated Graph; user must connect M365 once. */
  requiresDelegatedMailbox: boolean
  /** Refresh token stored for this portal user. */
  mailboxConnected: boolean
  emoji: string | null
}

export function useMe() {
  const me = useState<MeResponse | null>('auth-me', () => null)
  const meSessionId = useState<number>('auth-me-session', () => -1)

  function resetMe() {
    me.value = null
    meSessionId.value = -1
  }

  async function loadMe(force = false) {
    const { sessionId, token } = useAuth()
    if (!token.value) {
      resetMe()
      return null
    }

    if (!force && me.value && meSessionId.value === sessionId.value) {
      return me.value
    }

    const { fetchMe } = useAuth()
    me.value = await fetchMe()
    meSessionId.value = sessionId.value
    return me.value
  }

  function hasRole(role: string) {
    return me.value?.roles.includes(role) ?? false
  }

  const isEmployee = computed(() => hasRole('Employee'))
  const isApprover = computed(() => hasRole('Approver'))
  const isAdmin = computed(() => hasRole('Admin'))

  return { me, loadMe, resetMe, hasRole, isEmployee, isApprover, isAdmin }
}
