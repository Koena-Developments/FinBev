<script setup lang="ts">
const {
  needsMailboxConnection,
  mailboxConnectState,
  mailboxConnectUserCode,
  mailboxConnectVerificationUri,
  mailboxConnectMessage,
  mailboxConnectError,
  startMailboxConnect,
  cancelMailboxConnect
} = useMailboxConnect()

const props = withDefaults(
  defineProps<{
    /** Surface: full-width on portal · inset on finance/manager layouts */
    tone?: 'paper' | 'inset'
  }>(),
  { tone: 'paper' }
)

const wrapperClass = computed(() => {
  const base =
    'flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm shadow-sm'
  if (props.tone === 'inset') {
    return `${base} mx-4 mb-4 border-foodbev-mist bg-white text-foodbev-navy`
  }
  return `${base} mb-4 border-foodbev-blue/25 bg-foodbev-mist/80 text-foodbev-navy`
})

const buttonClass =
  'btn-foodbev shrink-0 px-3 py-1.5 text-xs shadow-none'
</script>

<template>
  <div v-if="needsMailboxConnection" :class="wrapperClass">
    <div class="min-w-0 flex-1">
      <p v-if="mailboxConnectState === 'starting'">Contacting Microsoft 365…</p>
      <div v-else-if="mailboxConnectState === 'polling'" class="space-y-1">
        <p>Enter this code at Microsoft, then sign in with your work account.</p>
        <p class="font-mono text-base font-bold tracking-wide text-foodbev-navy">{{ mailboxConnectUserCode }}</p>
        <a
          :href="mailboxConnectVerificationUri"
          target="_blank"
          rel="noopener noreferrer"
          class="font-medium text-foodbev-sky underline"
        >Open microsoft.com/devicelogin</a>
        <p v-if="mailboxConnectMessage" class="text-xs text-slate-500">{{ mailboxConnectMessage }}</p>
      </div>
      <p v-else>Connect your Microsoft 365 mailbox so emails send from you when you submit or approve.</p>
      <p v-if="mailboxConnectError" class="mt-1 text-sm font-medium text-red-600">{{ mailboxConnectError }}</p>
    </div>
    <button
      v-if="mailboxConnectState === 'polling'"
      type="button"
      :class="buttonClass"
      @click="cancelMailboxConnect"
    >Cancel</button>
    <button
      v-else
      type="button"
      :class="buttonClass"
      :disabled="mailboxConnectState === 'starting'"
      @click="startMailboxConnect"
    >{{ mailboxConnectState === 'starting' ? 'Starting…' : 'Connect mailbox' }}</button>
  </div>
</template>
