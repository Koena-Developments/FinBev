<script setup lang="ts">
const { needsMailboxConnection, startMailboxConnect } = useMailboxConnect()

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
    <span>Connect your Microsoft 365 mailbox so emails send from you when you submit or approve.</span>
    <button type="button" :class="buttonClass" @click="startMailboxConnect">Connect mailbox</button>
  </div>
</template>
