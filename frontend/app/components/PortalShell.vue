<script setup lang="ts">
type Department = { id: string; name: string }

const query = defineModel<string>('query', { default: '' })
const departmentId = defineModel<string>('departmentId', { default: '' })

const props = defineProps<{
  departments: Department[]
  showDepartments?: boolean
  /** Finance can filter the list. A department senior manager only sees their own department. */
  departmentsSelectable?: boolean
  /** Finance admins already have their own sidebar, so this page only supplies the content. */
  bare?: boolean
}>()

const route = useRoute()
const { data: signInMode } = useFetch<{ enabled: boolean }>('/api/microsoft-sign-in-enabled', {
  key: 'microsoft-sign-in-enabled'
})
const microsoftSignIn = computed(() => signInMode.value?.enabled === true)
const { me, loadMe, isApprover, isAdmin } = useMe()
const { signOut } = useAuth()
const { homePath } = usePersona()
const { onOpenAccountPanel } = useMailboxConnect()

const searchRef = ref<HTMLInputElement | null>(null)
const navOpen = ref(false)
const collapsed = ref(false)
const panel = ref<'org' | 'help' | 'bell' | 'account' | null>(null)

/** FoodBev-aligned accents for department dots (sky / blue / ice / navy tints) */
const dotColors = [
  '#4d8fd1',
  '#3d6eb5',
  '#9ec5ef',
  '#5b9fd4',
  '#1a4080',
  '#6b9fd4',
  '#2d5a8c',
  '#7eb3e0',
  '#163566',
  '#8eb8e8'
]

const menu = computed(() => {
  const items: { to: string; label: string; icon: 'dashboard' | 'requests' | 'upload' | 'approvals' | 'finance' | 'float' | 'admin' }[] = []
  const home = homePath()
  if (home !== '/requests') {
    items.push({ to: home, label: 'Dashboard', icon: 'dashboard' })
  }
  items.push({ to: '/requests', label: 'Requests', icon: 'requests' })
  items.push({ to: '/upload', label: 'Upload', icon: 'upload' })
  if (isApprover.value) {
    items.push({ to: '/approvals', label: 'Approvals', icon: 'approvals' })
  }
  if (isAdmin.value) {
    items.push({ to: '/finance', label: 'Finance', icon: 'finance' })
    items.push({ to: '/float', label: 'Float', icon: 'float' })
    items.push({ to: '/admin', label: 'Admin', icon: 'admin' })
  }
  return items
})

const orgTitle = computed(() => me.value?.departmentName || 'FoodBev')
const orgSubtitle = computed(() => me.value?.title || 'Petty cash')
const orgInitial = computed(() => orgTitle.value.trim().charAt(0).toUpperCase() || 'F')

const sidebarClass = computed(() => {
  if (navOpen.value) {
    return 'fixed bottom-3 left-3 top-3 flex rounded-[24px] border shadow-xl lg:static lg:inset-auto lg:flex lg:rounded-none lg:border-y-0 lg:border-l-0 lg:border-r lg:shadow-none'
  }
  if (collapsed.value) return 'hidden'
  return 'hidden lg:flex'
})

function itemActive(to: string, label: string) {
  if (label === 'Requests') return route.path === '/requests' || route.path.startsWith('/requests/')
  if (label === 'Upload') return route.path === '/upload' || route.path.startsWith('/upload/')
  if (to === '/finance') return route.path === '/finance' || route.path === '/finance/'
  return route.path === to || route.path.startsWith(`${to}/`)
}

const canSelectDepartments = computed(() => props.departmentsSelectable !== false && props.departments.length > 1)

function selectDepartment(id: string) {
  if (!canSelectDepartments.value) return
  departmentId.value = departmentId.value === id ? '' : id
}

function toggle(name: 'org' | 'help' | 'bell' | 'account') {
  const next = panel.value === name ? null : name
  panel.value = next
  if (name === 'account' && next === 'account') {
    onOpenAccountPanel()
  }
}

function closePanels() {
  panel.value = null
}

function onKey(event: KeyboardEvent) {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
    event.preventDefault()
    searchRef.value?.focus()
  }
}

onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKey)
})
</script>

<template>
  <div v-if="bare" class="p-6 lg:p-8">
    <slot />
  </div>
  <div v-else class="min-h-screen bg-foodbev-paper p-3 font-sans text-foodbev-navy sm:p-4" style="font-family: Inter, ui-sans-serif, system-ui, sans-serif">
    <div class="relative flex min-h-[calc(100vh-1.5rem)] overflow-hidden rounded-[28px] border border-foodbev-mist bg-white shadow-[0_10px_40px_rgba(26,64,128,0.08)] sm:min-h-[calc(100vh-2rem)]">
      <div v-if="navOpen" class="fixed inset-0 z-30 bg-foodbev-navy/25 lg:hidden" @click="navOpen = false" />

      <aside
        class="z-40 w-[260px] shrink-0 flex-col border-r border-foodbev-mist bg-white print:hidden"
        :class="sidebarClass"
      >
        <div class="flex items-center gap-2.5 px-5 pb-1 pt-5">
          <span class="flex h-9 w-9 items-center justify-center rounded-xl bg-foodbev-sky text-white">
            <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" d="M7 4.5h7.5L19 9v10.5a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1v-14a1 1 0 0 1 1-1Z" />
              <path stroke-linecap="round" d="M14 4.5V9h5M8.5 13h7M8.5 16.5h5" />
            </svg>
          </span>
          <span class="text-[15px] font-bold tracking-tight">Petty Cash</span>
          <button
            type="button"
            class="ml-auto hidden h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-foodbev-paper hover:text-foodbev-navy lg:inline-flex"
            aria-label="Collapse sidebar"
            @click="collapsed = true"
          >
            <svg viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="1.8">
              <rect x="4" y="4" width="6.5" height="6.5" rx="1.2" />
              <rect x="13.5" y="4" width="6.5" height="6.5" rx="1.2" />
              <rect x="4" y="13.5" width="6.5" height="6.5" rx="1.2" />
              <rect x="13.5" y="13.5" width="6.5" height="6.5" rx="1.2" />
            </svg>
          </button>
        </div>

        <div class="relative px-3 pt-4">
          <button
            type="button"
            class="flex w-full items-center gap-3 rounded-2xl border border-foodbev-mist bg-foodbev-paper px-3 py-2.5 text-left"
            @click="toggle('org')"
          >
            <span
              class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-sm font-semibold"
              :class="me?.emoji ? 'emoji-font bg-foodbev-paper text-[22px]' : 'bg-foodbev-navy text-white'"
            >
              {{ me?.emoji || orgInitial }}
            </span>
            <span class="min-w-0 flex-1">
              <span class="block truncate text-sm font-semibold">{{ orgTitle }}</span>
              <span class="block truncate text-xs text-slate-500">{{ orgSubtitle }}</span>
            </span>
            <svg viewBox="0 0 24 24" class="h-4 w-4 shrink-0 text-slate-400" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" stroke-linejoin="round" d="m8 10 4 4 4-4" />
            </svg>
          </button>
          <div v-if="panel === 'org'" class="absolute left-3 right-3 z-20 mt-1 rounded-xl border border-foodbev-mist bg-white p-1 shadow-lg">
            <p v-if="me" class="truncate px-3 py-2 text-xs text-slate-500"><PersonName :name="me.displayName" :emoji="me.emoji" /></p>
            <button type="button" class="w-full rounded-lg px-3 py-2 text-left text-sm text-slate-700 hover:bg-foodbev-paper" @click="signOut">
              Sign out
            </button>
          </div>
        </div>

        <div class="mt-6 px-5 text-[11px] font-semibold tracking-[0.14em] text-slate-400">MAIN MENU</div>
        <nav class="mt-2 space-y-0.5 px-3">
          <NuxtLink
            v-for="item in menu"
            :key="item.label"
            :to="item.to"
            class="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium"
            :class="itemActive(item.to, item.label) ? 'bg-foodbev-mist text-foodbev-sky' : 'text-slate-600 hover:bg-foodbev-paper'"
            @click="navOpen = false"
          >
            <svg v-if="item.icon === 'dashboard'" viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <rect x="3.5" y="3.5" width="7" height="7" rx="1.5" />
              <rect x="13.5" y="3.5" width="7" height="4.5" rx="1.5" />
              <rect x="13.5" y="11" width="7" height="9.5" rx="1.5" />
              <rect x="3.5" y="13.5" width="7" height="7" rx="1.5" />
            </svg>
            <svg v-else-if="item.icon === 'requests'" viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <circle cx="9" cy="8" r="2.4" />
              <path stroke-linecap="round" d="M4.5 17.5c.6-2.2 2.3-3.4 4.5-3.4s3.9 1.2 4.5 3.4" />
              <circle cx="16.5" cy="8.5" r="2" />
              <path stroke-linecap="round" d="M16 14.2c1.6.2 2.9 1.2 3.5 3.3" />
            </svg>
            <svg v-else-if="item.icon === 'upload'" viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 16V6" />
              <path stroke-linecap="round" stroke-linejoin="round" d="m8 9.5 4-4 4 4" />
              <path stroke-linecap="round" d="M5 19h14" />
            </svg>
            <svg v-else-if="item.icon === 'approvals'" viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" stroke-linejoin="round" d="m5 12 4.2 4.2L19 7.5" />
            </svg>
            <svg v-else-if="item.icon === 'finance'" viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <rect x="3.5" y="6" width="17" height="12.5" rx="2" />
              <path stroke-linecap="round" d="M3.5 10h17" />
            </svg>
            <svg v-else-if="item.icon === 'float'" viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" d="M12 4v16M7 8.5h7.2a2.8 2.8 0 0 1 0 5.6H8.8a2.8 2.8 0 0 0 0 5.6H17" />
            </svg>
            <svg v-else viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <circle cx="12" cy="12" r="3" />
              <path stroke-linecap="round" d="M12 3.5v2.2M12 18.3v2.2M3.5 12h2.2M18.3 12h2.2M6 6l1.6 1.6M16.4 16.4 18 18M18 6l-1.6 1.6M7.6 16.4 6 18" />
            </svg>
            {{ item.label }}
          </NuxtLink>
        </nav>

        <template v-if="props.showDepartments">
        <div class="mt-6 flex items-center px-5 text-[11px] font-semibold tracking-[0.14em] text-slate-400">DEPARTMENT</div>
        <div class="mt-2 max-h-56 space-y-0.5 overflow-y-auto px-3 pb-2">
          <component
            :is="canSelectDepartments ? 'button' : 'div'"
            v-for="(department, index) in props.departments"
            :key="department.id"
            :type="canSelectDepartments ? 'button' : undefined"
            class="flex w-full items-center gap-3 rounded-xl px-3 py-2 text-left text-sm"
            :class="canSelectDepartments && departmentId === department.id ? 'bg-foodbev-mist font-semibold text-foodbev-navy' : canSelectDepartments ? 'text-slate-500 hover:bg-foodbev-paper' : 'font-semibold text-foodbev-navy'"
            @click="selectDepartment(department.id)"
          >
            <span class="h-2 w-2 shrink-0 rounded-full" :style="{ backgroundColor: dotColors[index % dotColors.length] }" />
            <span class="truncate">{{ department.name }}</span>
          </component>
          <p v-if="!props.departments.length" class="px-3 py-2 text-sm text-slate-400">No departments</p>
        </div>
        </template>

        <div class="mt-4 px-5 text-[11px] font-semibold tracking-[0.14em] text-slate-400">OTHER</div>
        <div class="relative mt-2 space-y-0.5 px-3 pb-5">
          <NuxtLink
            v-if="!microsoftSignIn"
            to="/change-password"
            class="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-slate-600 hover:bg-foodbev-paper"
          >
            <svg viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <circle cx="12" cy="12" r="3" />
              <path stroke-linecap="round" d="M12 3.5v2.2M12 18.3v2.2M3.5 12h2.2M18.3 12h2.2M6 6l1.6 1.6M16.4 16.4 18 18M18 6l-1.6 1.6M7.6 16.4 6 18" />
            </svg>
            Setting
          </NuxtLink>
          <button
            type="button"
            class="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-medium text-slate-600 hover:bg-foodbev-paper"
            @click="toggle('help')"
          >
            <svg viewBox="0 0 24 24" class="h-[18px] w-[18px]" fill="none" stroke="currentColor" stroke-width="1.8">
              <circle cx="12" cy="12" r="8.5" />
              <path stroke-linecap="round" d="M9.5 9.5a2.5 2.5 0 1 1 3.2 2.4c-.7.3-1.2.9-1.2 1.6V14" />
              <circle cx="12" cy="17" r="0.6" fill="currentColor" />
            </svg>
            Help Center
          </button>
          <div v-if="panel === 'help'" class="mt-1 rounded-xl border border-foodbev-mist bg-white p-3 text-xs leading-5 text-slate-600 shadow-lg">
            Each request is capped at R2,000. Submit a signed form so your department senior manager, then Finance, can approve it before cash is disbursed.
          </div>
        </div>
      </aside>

      <div class="flex min-w-0 flex-1 flex-col" @click="closePanels">
        <header class="flex items-center gap-3 px-5 pb-2 pt-5 print:hidden sm:px-8">
          <button
            type="button"
            class="inline-flex h-10 w-10 items-center justify-center rounded-xl border border-foodbev-mist text-slate-600 lg:hidden"
            aria-label="Open menu"
            @click.stop="navOpen = true"
          >
            <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" d="M4 7h16M4 12h16M4 17h16" />
            </svg>
          </button>
          <button
            v-if="collapsed"
            type="button"
            class="hidden h-10 w-10 items-center justify-center rounded-xl border border-foodbev-mist text-slate-600 lg:inline-flex"
            aria-label="Expand sidebar"
            @click.stop="collapsed = false"
          >
            <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" d="M4 7h16M4 12h16M4 17h16" />
            </svg>
          </button>

          <label class="relative block min-w-0 max-w-xl flex-1">
            <svg viewBox="0 0 24 24" class="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" fill="none" stroke="currentColor" stroke-width="1.8">
              <circle cx="11" cy="11" r="6.5" />
              <path stroke-linecap="round" d="m16 16 4 4" />
            </svg>
            <input
              ref="searchRef"
              v-model="query"
              type="search"
              placeholder="Search keyword..."
              class="h-11 w-full rounded-xl border border-foodbev-mist bg-white pl-11 pr-16 text-sm text-foodbev-navy outline-none placeholder:text-slate-400 focus:border-foodbev-blue focus:ring-1 focus:ring-foodbev-blue"
              @click.stop
            />
            <kbd class="pointer-events-none absolute right-3 top-1/2 hidden -translate-y-1/2 rounded-md border border-foodbev-mist bg-foodbev-paper px-1.5 py-0.5 text-[11px] font-medium text-slate-400 sm:inline">
              Ctrl K
            </kbd>
          </label>

          <div class="ml-auto flex items-center gap-2">
            <ThemeToggle />
            <div class="relative">
              <button
                type="button"
                class="inline-flex h-10 w-10 items-center justify-center rounded-xl text-slate-500 hover:bg-foodbev-paper"
                aria-label="Notifications"
                @click.stop="toggle('bell')"
              >
                <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="1.8">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 16.5V10a6 6 0 1 1 12 0v6.5l1.2 1.5H4.8L6 16.5Z" />
                  <path stroke-linecap="round" d="M10 19a2 2 0 0 0 4 0" />
                </svg>
              </button>
              <div v-if="panel === 'bell'" class="absolute right-0 z-20 mt-2 w-64 rounded-xl border border-foodbev-mist bg-white p-3 text-sm text-slate-600 shadow-lg" @click.stop>
                <p v-if="isApprover">Review requests waiting on you from Approvals.</p>
                <p v-else>You're up to date. New activity on your requests shows in the list below.</p>
                <NuxtLink v-if="isApprover" to="/approvals" class="mt-2 inline-block text-sm font-medium text-foodbev-sky hover:text-foodbev-blue">Open approvals</NuxtLink>
              </div>
            </div>
            <div class="relative">
              <button
                type="button"
                class="inline-flex h-10 w-10 items-center justify-center rounded-xl text-slate-500 hover:bg-foodbev-paper"
                aria-label="Account"
                @click.stop="toggle('account')"
              >
                <span v-if="me?.emoji" class="emoji-font text-[22px]">{{ me.emoji }}</span>
                <svg v-else viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="1.8">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M14 5h5v5" />
                  <path stroke-linecap="round" d="M19 5 11 13" />
                  <path stroke-linecap="round" stroke-linejoin="round" d="M10 6H7a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h9a2 2 0 0 0 2-2v-3" />
                </svg>
              </button>
              <div v-if="panel === 'account'" class="absolute right-0 z-20 mt-2 w-80 max-w-[calc(100vw-2rem)] rounded-xl border border-foodbev-mist bg-white p-3 shadow-lg" @click.stop>
                <AccountEmoji />
                <p v-if="me" class="truncate px-1 py-1 text-sm font-medium text-foodbev-navy"><PersonName :name="me.displayName" :emoji="me.emoji" /></p>
                <MailboxConnectPanel variant="portal" />
                <button type="button" class="mt-2 w-full rounded-lg px-3 py-2 text-left text-sm text-slate-700 hover:bg-foodbev-paper" @click="signOut">
                  Sign out
                </button>
              </div>
            </div>
          </div>
        </header>

        <main class="min-w-0 flex-1 px-5 pb-8 pt-4 sm:px-8">
          <MailboxConnectBanner />
          <slot />
        </main>
      </div>
    </div>
  </div>
</template>
