<script setup lang="ts">
const route = useRoute()
const { me, loadMe, resetMe } = useMe()
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

const links = [
  { to: '/finance', label: 'Overview', icon: '◉' },
  { to: '/requests', label: 'Requests', icon: '☰' },
  { to: '/upload', label: 'Upload', icon: '↑' },
  { to: '/approvals', label: 'Approvals', icon: '✓' },
  { to: '/finance/documents', label: 'Claim documents', icon: '▤' },
  { to: '/float', label: 'Float & disburse', icon: '◈' },
  { to: '/admin', label: 'Admin', icon: '⚙' }
]

function linkActive(path: string) {
  if (path === '/finance') {
    return route.path === '/finance' || route.path === '/finance/'
  }
  return route.path === path || route.path.startsWith(`${path}/`)
}
</script>

<template>
  <div class="flex min-h-screen bg-foodbev-paper">
    <aside class="flex w-56 shrink-0 flex-col bg-foodbev-navy-dark px-4 py-6 text-foodbev-ice print:hidden">
      <NuxtLink to="/finance" class="mb-6 block">
        <img src="/images/foodbev-logo.png" alt="FoodBev" class="mb-3 h-10 w-auto object-contain brightness-0 invert" />
        <span class="text-sm font-semibold tracking-wide text-white">Petty Cash</span>
      </NuxtLink>
      <nav class="flex flex-1 flex-col gap-1 text-sm">
        <NuxtLink
          v-for="item in links"
          :key="item.to"
          :to="item.to"
          class="flex items-center gap-2 rounded-lg px-3 py-2 transition"
          :class="linkActive(item.to) ? 'bg-foodbev-sky text-white' : 'text-foodbev-ice hover:bg-foodbev-navy hover:text-white'"
        >
          <span class="text-xs opacity-80">{{ item.icon }}</span>
          {{ item.label }}
        </NuxtLink>
      </nav>
      <MailboxConnectPanel variant="financeSidebar" />
      <div class="border-t border-foodbev-navy pt-4 text-xs">
        <div class="mb-3">
          <ThemeToggle />
        </div>
        <AccountEmoji tone="dark" class="mb-3" />
        <p v-if="me" class="mb-2 text-sm text-white"><PersonName :name="me.displayName" :emoji="me.emoji" /></p>
        <button type="button" class="text-foodbev-ice hover:text-white" @click="signOut">Sign out</button>
      </div>
    </aside>
    <div class="flex min-w-0 flex-1 flex-col">
      <MailboxConnectBanner tone="inset" />
      <slot />
    </div>
  </div>
</template>
