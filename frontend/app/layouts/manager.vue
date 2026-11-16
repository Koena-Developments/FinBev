<script setup lang="ts">
const { loadMe, resetMe } = useMe()
const { token, sessionId, signOut } = useAuth()

async function refreshProfile() {
  if (!token.value) {
    resetMe()
    return
  }
  try {
    await loadMe(true)
  } catch {
    await signOut()
  }
}

if (import.meta.client && token.value) {
  await refreshProfile()
}

watch(sessionId, () => {
  if (import.meta.client) {
    refreshProfile()
  }
})
</script>

<template>
  <PortalShell :departments="[]" :show-departments="false">
    <slot />
  </PortalShell>
</template>
