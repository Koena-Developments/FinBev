/**
 * Shared Microsoft 365 device-code connect flow for delegated Graph mail.
 * Uses useState so banner + sidebar/card stay in sync on the same page.
 */

let mailboxPollTimer: ReturnType<typeof setInterval> | null = null

export function useMailboxConnect() {
  const api = useApi()
  const { loadMe, me } = useMe()

  const mailboxConnectState = useState<'idle' | 'starting' | 'polling' | 'success'>(
    'mailbox-connect-state',
    () => 'idle'
  )
  const mailboxConnectUserCode = useState<string>('mailbox-connect-user-code', () => '')
  const mailboxConnectVerificationUri = useState<string>(
    'mailbox-connect-verification-uri',
    () => 'https://microsoft.com/devicelogin'
  )
  const mailboxConnectMessage = useState<string>('mailbox-connect-message', () => '')
  const mailboxConnectError = useState<string>('mailbox-connect-error', () => '')

  type DeviceCodeStart = {
    userCode: string
    verificationUri: string
    message: string
    expiresIn: number
    intervalSeconds: number
  }

  const needsMailboxConnection = computed(
    () => !!(me.value?.requiresDelegatedMailbox && !me.value?.mailboxConnected)
  )

  function stopMailboxPoll() {
    if (mailboxPollTimer) {
      clearInterval(mailboxPollTimer)
      mailboxPollTimer = null
    }
  }

  async function pollMailboxOnce() {
    if (mailboxConnectState.value !== 'polling') return
    try {
      const r = await api<{ status: string; error: string | null }>('/api/mailbox/microsoft/device-code/poll', {
        method: 'POST',
        body: '{}'
      })
      if (r.status === 'success') {
        stopMailboxPoll()
        mailboxConnectState.value = 'success'
        await loadMe(true)
        setTimeout(() => {
          mailboxConnectState.value = 'idle'
          mailboxConnectUserCode.value = ''
        }, 5000)
      } else if (r.status === 'error') {
        stopMailboxPoll()
        mailboxConnectError.value = r.error ?? 'Microsoft 365 sign-in did not finish. Start the mailbox connection again.'
        mailboxConnectState.value = 'idle'
      }
    } catch (e) {
      mailboxConnectError.value = e instanceof Error ? e.message : 'We could not check whether Microsoft 365 sign-in finished. Start the mailbox connection again.'
      stopMailboxPoll()
      mailboxConnectState.value = 'idle'
    }
  }

  async function startMailboxConnect() {
    mailboxConnectError.value = ''
    stopMailboxPoll()
    mailboxConnectState.value = 'starting'
    try {
      const start = await api<DeviceCodeStart>('/api/mailbox/microsoft/device-code/start', {
        method: 'POST',
        body: '{}'
      })
      mailboxConnectUserCode.value = start.userCode
      mailboxConnectVerificationUri.value = start.verificationUri || 'https://microsoft.com/devicelogin'
      mailboxConnectMessage.value = start.message
      mailboxConnectState.value = 'polling'
      const intervalMs = Math.max((start.intervalSeconds || 5) * 1000, 3000)
      mailboxPollTimer = setInterval(() => {
        void pollMailboxOnce()
      }, intervalMs)
      await pollMailboxOnce()
    } catch (e) {
      mailboxConnectError.value = e instanceof Error ? e.message : 'We could not start Microsoft 365 sign-in. Try again in a moment.'
      mailboxConnectState.value = 'idle'
    }
  }

  function cancelMailboxConnect() {
    stopMailboxPoll()
    mailboxConnectState.value = 'idle'
    mailboxConnectUserCode.value = ''
    mailboxConnectError.value = ''
  }

  function onOpenAccountPanel() {
    void loadMe(true)
  }

  return {
    me,
    loadMe,
    needsMailboxConnection,
    mailboxConnectState,
    mailboxConnectUserCode,
    mailboxConnectVerificationUri,
    mailboxConnectMessage,
    mailboxConnectError,
    startMailboxConnect,
    cancelMailboxConnect,
    onOpenAccountPanel
  }
}
