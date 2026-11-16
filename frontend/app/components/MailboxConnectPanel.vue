<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    /** Light surfaces (default/manager) · account popover (portal) · finance navy sidebar */
    variant?: 'foodbev' | 'portal' | 'financeSidebar'
  }>(),
  { variant: 'foodbev' }
)

const {
  me,
  mailboxConnectState,
  mailboxConnectUserCode,
  mailboxConnectVerificationUri,
  mailboxConnectMessage,
  mailboxConnectError,
  startMailboxConnect,
  cancelMailboxConnect
} = useMailboxConnect()

const mutedClass = computed(() =>
  props.variant === 'financeSidebar' ? 'text-foodbev-ice/90' : 'text-slate-500'
)

const cardClass = computed(() => {
  if (props.variant === 'portal') {
    return 'mt-2 rounded-lg border border-foodbev-mist bg-foodbev-paper p-3 text-xs leading-relaxed text-slate-600'
  }
  if (props.variant === 'financeSidebar') {
    return 'mt-3 rounded-lg border border-foodbev-ice/25 bg-foodbev-navy/60 p-3 text-xs leading-relaxed text-foodbev-ice'
  }
  return 'mt-3 rounded-lg border border-foodbev-mist bg-white p-3 text-xs leading-relaxed text-slate-600 shadow-sm'
})

const titleClass = computed(() => {
  if (props.variant === 'financeSidebar') return 'font-semibold text-white'
  return 'font-semibold text-foodbev-navy'
})

const codeClass = computed(() => {
  if (props.variant === 'financeSidebar') return 'font-mono text-sm font-bold tracking-wide text-foodbev-ice'
  return 'font-mono text-sm font-bold tracking-wide text-foodbev-navy'
})

const linkClass = computed(() => {
  if (props.variant === 'financeSidebar') return 'font-medium text-foodbev-ice underline decoration-foodbev-sky/80 hover:text-white'
  return 'font-medium text-foodbev-sky underline decoration-foodbev-sky/50 hover:text-foodbev-blue'
})

const primaryButtonClass = computed(() => {
  return 'w-full rounded-lg bg-foodbev-sky px-3 py-2 text-xs font-semibold text-white transition hover:bg-foodbev-sky-light'
})

const cancelClass = computed(() =>
  props.variant === 'financeSidebar'
    ? 'text-xs text-foodbev-ice/80 underline hover:text-white'
    : 'text-xs text-slate-500 underline hover:text-foodbev-navy'
)

const errorClass = computed(() =>
  props.variant === 'financeSidebar' ? 'mt-2 text-sm text-rose-200' : 'mt-2 text-sm text-red-600'
)
</script>

<template>
  <div v-if="me?.requiresDelegatedMailbox" :class="cardClass">
    <p :class="titleClass">Microsoft 365 mailbox</p>
    <p v-if="me.mailboxConnected" class="mt-1 font-medium text-foodbev-sky">Connected. Notifications send from your work email.</p>
    <template v-else>
      <p class="mt-1">Connect once so approval emails send from your FoodBev account.</p>
      <div v-if="mailboxConnectState === 'idle'" class="mt-2">
        <button type="button" :class="primaryButtonClass" @click="startMailboxConnect">
          Connect mailbox
        </button>
      </div>
      <div v-else-if="mailboxConnectState === 'polling'" class="mt-2 space-y-2">
        <p :class="codeClass">{{ mailboxConnectUserCode }}</p>
        <p>
          Open
          <a
            :href="mailboxConnectVerificationUri"
            target="_blank"
            rel="noopener noreferrer"
            :class="linkClass"
          >
            microsoft.com/devicelogin
          </a>
          and sign in with your work account.
        </p>
        <p v-if="mailboxConnectMessage" class="text-[11px]" :class="mutedClass">{{ mailboxConnectMessage }}</p>
        <button type="button" :class="cancelClass" @click="cancelMailboxConnect">Cancel</button>
      </div>
      <p v-else-if="mailboxConnectState === 'success'" class="mt-1 font-medium text-foodbev-sky">Mailbox connected.</p>
      <p v-if="mailboxConnectError" :class="errorClass">{{ mailboxConnectError }}</p>
    </template>
  </div>
</template>
