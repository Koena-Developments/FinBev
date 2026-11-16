<script setup lang="ts">
import { formatZar } from '~/utils/format'

definePageMeta({ layout: false })

useHead({
  title: 'Requests',
  link: [
    {
      rel: 'stylesheet',
      href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap'
    }
  ]
})

type Summary = {
  id: string
  reason: string
  amount: number
  status: string
  expenseDate: string
  departmentName: string
  createdAtUtc: string
  claimantName?: string
  claimantEmoji?: string | null
}

type DepartmentHistory = {
  managedDepartmentNames: string[]
  recentRequests: {
    id: string
    reason: string
    amount: number
    status: string
    claimantName: string
    claimantEmoji?: string | null
    submittedAtUtc: string | null
    expenseDate: string
    departmentName: string
    createdAtUtc: string
  }[]
}

type Department = { id: string; name: string }
type TabId = 'all' | 'pending' | 'approved' | 'draft' | 'rejected'

const api = useApi()
const { me, loadMe, isApprover, isAdmin } = useMe()
const { isFinanceAdmin } = usePersona()
await loadMe()

watchEffect(() => {
  if (isFinanceAdmin.value) setPageLayout('finance')
})

const isDepartmentManager = computed(() => isApprover.value && !isAdmin.value)
const canBrowseDepartments = computed(() => isAdmin.value)
const query = ref('')
const departmentId = ref('')
const tab = ref<TabId>('all')
const filterOpen = ref(false)

const managedDepartments = ref<Department[]>([])

const { data: requests, error, pending } = await useAuthedAsyncData('my-requests', async () => {
  const mine = await api<Summary[]>('/api/requests')
  if (!isDepartmentManager.value) return mine

  const now = new Date()
  const overview = await api<DepartmentHistory>(
    `/api/manager/overview?year=${now.getFullYear()}&month=${now.getMonth() + 1}`
  ).catch(() => null)
  if (!overview) return mine

  const names = overview.managedDepartmentNames?.length
    ? overview.managedDepartmentNames
    : me.value?.departmentName
      ? [me.value.departmentName]
      : []
  managedDepartments.value = names.map((name) => ({ id: name, name }))

  const history: Summary[] = overview.recentRequests
    .filter((item) => names.length === 0 || names.includes(item.departmentName))
    .map((item) => ({
    id: item.id,
    reason: item.reason,
    amount: item.amount,
    status: item.status,
    expenseDate: item.expenseDate,
    departmentName: item.departmentName,
    createdAtUtc: item.submittedAtUtc ?? item.createdAtUtc,
    claimantName: item.claimantName,
    claimantEmoji: item.claimantEmoji
  }))
  const seen = new Set(history.map((item) => item.id))
  const drafts = mine
    .filter((item) => item.status === 'Draft' && !seen.has(item.id))
    .map((item) => ({
      ...item,
      claimantName: item.claimantName || me.value?.displayName,
      claimantEmoji: item.claimantEmoji || me.value?.emoji
    }))
  return [...history, ...drafts]
})
const { data: departments } = await useAuthedAsyncData('departments', () => {
  if (!isAdmin.value) return Promise.resolve([] as Department[])
  return api<Department[]>('/api/departments').catch(() => [] as Department[])
})

const sidebarDepartments = computed(() => {
  if (isAdmin.value) return departments.value ?? []
  if (!isDepartmentManager.value) return []
  if (managedDepartments.value.length) return managedDepartments.value
  if (!me.value?.departmentId) return []
  return [{ id: me.value.departmentId, name: me.value.departmentName }]
})

const departmentLabel = computed(() => sidebarDepartments.value.map((department) => department.name).join(', ') || 'your department')

const tabs = [
  { id: 'all' as const, label: 'All requests' },
  { id: 'pending' as const, label: 'Pending' },
  { id: 'approved' as const, label: 'Approved' }
]

const filterOptions: { id: TabId; label: string }[] = [
  { id: 'all', label: 'All requests' },
  { id: 'pending', label: 'Pending' },
  { id: 'approved', label: 'Approved' },
  { id: 'draft', label: 'Drafts' },
  { id: 'rejected', label: 'Rejected' }
]

const deptPills = [
  'bg-[#e8f3ff] text-[#3b82f6]',
  'bg-[#e7f8ef] text-[#15966a]',
  'bg-[#f3e8ff] text-[#7c3aed]',
  'bg-[#fff4e5] text-[#d97706]',
  'bg-[#e0f2fe] text-[#0284c7]',
  'bg-[#fce7f3] text-[#db2777]'
]

const avatarTones = [
  'bg-[#ffe4e6] text-[#be123c]',
  'bg-[#dbeafe] text-[#1d4ed8]',
  'bg-[#dcfce7] text-[#15803d]',
  'bg-[#fef3c7] text-[#b45309]',
  'bg-[#ede9fe] text-[#6d28d9]',
  'bg-[#ffedd5] text-[#c2410c]'
]

function toneIndex(value: string, size: number) {
  let hash = 0
  for (const char of value) hash = (hash + char.charCodeAt(0)) % size
  return hash
}

function rowEmoji(item: Summary) {
  if (item.claimantEmoji) return item.claimantEmoji
  if (me.value?.emoji && (!item.claimantName || item.claimantName === me.value.displayName)) return me.value.emoji
  return ''
}

function initials(text: string) {
  const parts = text.trim().split(/\s+/).filter(Boolean)
  const first = parts[0]?.[0] ?? '?'
  const second = parts[1]?.[0] ?? parts[0]?.[1] ?? ''
  return `${first}${second}`.toUpperCase()
}

function matchesTab(status: string) {
  if (tab.value === 'pending') return status === 'Submitted' || status === 'PendingFinanceApproval'
  if (tab.value === 'approved') return status === 'Approved' || status === 'PendingDisbursement' || status === 'Disbursed'
  if (tab.value === 'draft') return status === 'Draft'
  if (tab.value === 'rejected') return status === 'Rejected' || status === 'Cancelled'
  return true
}

const filtered = computed(() => {
  const keyword = query.value.trim().toLowerCase()
  const departmentName = departments.value?.find((department) => department.id === departmentId.value)?.name
  return [...(requests.value ?? [])]
    .filter((item) => matchesTab(item.status))
    .filter((item) => !departmentName || item.departmentName === departmentName)
    .filter((item) => {
      if (!keyword) return true
      return [item.reason, item.claimantName, item.departmentName, item.status, formatZar(item.amount)].join(' ').toLowerCase().includes(keyword)
    })
    .sort((a, b) => +new Date(b.createdAtUtc) - +new Date(a.createdAtUtc))
})

function statusMeta(status: string) {
  if (status === 'Submitted' || status === 'PendingFinanceApproval') {
    return { label: 'Pending', className: 'bg-[#fdecee] text-[#e11d48]' }
  }
  if (status === 'Approved' || status === 'PendingDisbursement') {
    return { label: 'Approved', className: 'bg-[#e7f8ef] text-[#16a34a]' }
  }
  if (status === 'Disbursed') return { label: 'Disbursed', className: 'bg-[#e7f8ef] text-[#16a34a]' }
  if (status === 'Rejected') return { label: 'Rejected', className: 'bg-[#fdecee] text-[#e11d48]' }
  if (status === 'Cancelled') return { label: 'Cancelled', className: 'bg-gray-100 text-gray-500' }
  if (status === 'Draft') return { label: 'Draft', className: 'bg-[#eef2ff] text-[#5458f2]' }
  return { label: status, className: 'bg-gray-100 text-gray-600' }
}

function needsAttention(status: string) {
  return status === 'Draft' || status === 'Submitted' || status === 'PendingFinanceApproval'
}

function formatExpenseDate(value: string) {
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return value
  return new Date(year, month - 1, day).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

function formatCreated(value: string) {
  return new Date(value).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

function chooseTab(id: TabId) {
  tab.value = id
  filterOpen.value = false
}

function exportCsv() {
  const header = [
    'Reason',
    ...(isDepartmentManager.value ? ['Requested by'] : []),
    'Department',
    'Expense date',
    'Created',
    'Amount',
    'Status'
  ]
  const lines = [
    header,
    ...filtered.value.map((item) => [
      item.reason,
      ...(isDepartmentManager.value ? [item.claimantName ?? ''] : []),
      item.departmentName,
      formatExpenseDate(item.expenseDate),
      formatCreated(item.createdAtUtc),
      formatZar(item.amount),
      statusMeta(item.status).label
    ])
  ]
  const csv = lines
    .map((row) => row.map((cell) => `"${String(cell).replaceAll('"', '""')}"`).join(','))
    .join('\r\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = 'petty-cash-requests.csv'
  link.click()
  URL.revokeObjectURL(url)
}

function openRequest(id: string) {
  return navigateTo(`/requests/${id}`)
}
</script>

<template>
  <PortalShell
    v-model:query="query"
    v-model:department-id="departmentId"
    :departments="sidebarDepartments"
    :show-departments="sidebarDepartments.length > 0"
    :departments-selectable="isAdmin"
    :bare="isFinanceAdmin"
  >
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div class="flex items-center gap-3">
        <span class="flex h-11 w-11 items-center justify-center rounded-full bg-[#eef0ff] text-[#5458f2]">
          <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="1.8">
            <circle cx="12" cy="8" r="3" />
            <path stroke-linecap="round" d="M5.5 18.5c.8-3 3-4.5 6.5-4.5s5.7 1.5 6.5 4.5" />
          </svg>
        </span>
        <div>
          <h1 class="text-[22px] font-semibold tracking-tight text-[#1e2430]">Requests</h1>
          <p class="text-sm text-[#98a2b3]">
            {{ isDepartmentManager ? `Who requested petty cash in ${departmentLabel}, and how much` : 'Manage your petty cash requests' }}
          </p>
        </div>
      </div>
      <div class="flex items-center gap-2">
        <button
          type="button"
          class="inline-flex h-10 items-center gap-2 rounded-xl border border-[#e6e8ef] bg-white px-4 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
          @click="exportCsv"
        >
          <svg viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 16V5" />
            <path stroke-linecap="round" stroke-linejoin="round" d="m7.5 9 4.5-4.5L16.5 9" />
            <path stroke-linecap="round" d="M5 19h14" />
          </svg>
          Export
        </button>
        <NuxtLink
          to="/requests/new"
          class="inline-flex h-10 items-center gap-1.5 rounded-xl bg-[#5458f2] px-4 text-sm font-semibold text-white shadow-sm hover:bg-[#4548e0]"
        >
          <svg viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" d="M12 5v14M5 12h14" />
          </svg>
          Add request
        </NuxtLink>
      </div>
    </div>

    <div class="mt-7 flex gap-8 overflow-x-auto border-b border-[#eef1f6]">
      <button
        v-for="item in tabs"
        :key="item.id"
        type="button"
        class="-mb-px flex items-center gap-2 whitespace-nowrap border-b-2 pb-3 text-sm font-medium"
        :class="tab === item.id ? 'border-[#5458f2] text-[#5458f2]' : 'border-transparent text-gray-500 hover:text-gray-700'"
        @click="chooseTab(item.id)"
      >
        <svg v-if="item.id === 'all'" viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="1.8">
          <circle cx="8" cy="9" r="2" />
          <circle cx="16" cy="9" r="2" />
          <path stroke-linecap="round" d="M4.5 17c.5-2 1.8-3 3.5-3s3 1 3.5 3M12.5 17c.5-2 1.8-3 3.5-3s3 1 3.5 3" />
        </svg>
        <svg v-else-if="item.id === 'pending'" viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="1.8">
          <circle cx="12" cy="12" r="8" />
          <path stroke-linecap="round" d="M12 8v4l2.5 2" />
        </svg>
        <svg v-else viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="1.8">
          <path stroke-linecap="round" stroke-linejoin="round" d="m5 12 4.2 4.2L19 7.5" />
        </svg>
        {{ item.label }}
      </button>
    </div>

    <div class="mt-6 flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-[17px] font-semibold text-[#1e2430]">Petty cash requests</h2>
      <div class="flex items-center gap-2">
        <label class="relative block">
          <svg viewBox="0 0 24 24" class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" fill="none" stroke="currentColor" stroke-width="1.8">
            <circle cx="11" cy="11" r="6.5" />
            <path stroke-linecap="round" d="m16 16 4 4" />
          </svg>
          <input
            v-model="query"
            type="search"
            placeholder="Search keyword..."
            class="h-10 w-52 rounded-xl border border-[#e6e8ef] bg-white pl-9 pr-3 text-sm outline-none placeholder:text-gray-400 focus:border-[#c7c9f8] sm:w-64"
          />
        </label>
        <div class="relative">
          <button
            type="button"
            class="inline-flex h-10 items-center gap-2 rounded-xl border bg-white px-3.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
            :class="filterOpen || tab !== 'all' || departmentId ? 'border-[#c7c9f8] text-[#5458f2]' : 'border-[#e6e8ef]'"
            @click="filterOpen = !filterOpen"
          >
            <svg viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" d="M4 6h16l-6 7.2V19l-4-2v-3.8L4 6Z" />
            </svg>
            Filter
          </button>
          <div v-if="filterOpen" class="fixed inset-0 z-20" @click="filterOpen = false" />
          <div v-if="filterOpen" class="absolute right-0 z-30 mt-2 w-64 rounded-2xl border border-[#eef0f4] bg-white p-3 shadow-lg">
            <p class="px-1 text-[11px] font-semibold tracking-[0.14em] text-gray-400">STATUS</p>
            <div class="mt-2 flex flex-wrap gap-1.5">
              <button
                v-for="option in filterOptions"
                :key="option.id"
                type="button"
                class="rounded-full px-2.5 py-1 text-xs font-medium"
                :class="tab === option.id ? 'bg-[#eef0ff] text-[#5458f2]' : 'bg-[#f6f7fb] text-gray-600 hover:bg-gray-100'"
                @click="chooseTab(option.id)"
              >
                {{ option.label }}
              </button>
            </div>
            <template v-if="canBrowseDepartments">
              <p class="mt-3 px-1 text-[11px] font-semibold tracking-[0.14em] text-gray-400">DEPARTMENT</p>
              <button
                type="button"
                class="mt-2 w-full rounded-lg px-2 py-1.5 text-left text-sm"
                :class="!departmentId ? 'bg-[#eef0ff] font-medium text-[#5458f2]' : 'text-gray-600 hover:bg-gray-50'"
                @click="departmentId = ''"
              >
                All departments
              </button>
              <div class="mt-1 max-h-40 overflow-y-auto">
                <button
                  v-for="department in departments ?? []"
                  :key="department.id"
                  type="button"
                  class="w-full truncate rounded-lg px-2 py-1.5 text-left text-sm"
                  :class="departmentId === department.id ? 'bg-[#eef0ff] font-medium text-[#5458f2]' : 'text-gray-600 hover:bg-gray-50'"
                  @click="departmentId = department.id"
                >
                  {{ department.name }}
                </button>
              </div>
            </template>
          </div>
        </div>
      </div>
    </div>

    <p v-if="error" class="mt-4 text-sm text-red-600">{{ error.message }}</p>

    <div class="mt-5 overflow-x-auto">
      <div class="min-w-[920px]">
        <div class="request-grid rounded-xl bg-[#f6f7fb] px-4 py-3 text-[13px] font-medium text-[#98a2b3]" :class="{ 'with-requester': isDepartmentManager }">
          <span>Request</span>
          <span v-if="isDepartmentManager">Requested by</span>
          <span>Department</span>
          <span>Expense date</span>
          <span>Created</span>
          <span>Amount</span>
          <span>Status</span>
          <span />
        </div>

        <div v-if="pending && !requests" class="mt-3 space-y-2.5">
          <div v-for="row in 4" :key="row" class="h-[68px] animate-pulse rounded-2xl bg-[#f6f7fb]" />
        </div>

        <p v-else-if="!filtered.length" class="mt-3 rounded-2xl border border-[#eef0f4] px-4 py-10 text-center text-sm text-gray-400">
          {{ requests?.length ? 'No requests match your filters.' : 'No requests yet.' }}
        </p>

        <div v-else class="mt-3 space-y-2.5">
          <div
            v-for="item in filtered"
            :key="item.id"
            class="request-grid cursor-pointer rounded-2xl border border-[#eef0f4] bg-white px-4 py-3.5 transition hover:border-[#e3e7ef] hover:bg-[#fcfcfe]"
            :class="{ 'with-requester': isDepartmentManager }"
            @click="openRequest(item.id)"
          >
            <div class="flex min-w-0 items-center gap-3">
              <span
                class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-semibold"
                :class="rowEmoji(item) ? 'emoji-font bg-[#f4f6fb] text-[22px]' : avatarTones[toneIndex(item.reason, avatarTones.length)]"
              >
                {{ rowEmoji(item) || initials(item.claimantName || item.reason) }}
              </span>
              <span class="flex min-w-0 items-center gap-1.5">
                <span class="truncate text-sm font-medium text-[#1e2430]" :title="item.reason">{{ item.reason }}</span>
                <span v-if="needsAttention(item.status)" class="h-1.5 w-1.5 shrink-0 rounded-full bg-[#f43f5e]" />
              </span>
            </div>
            <span v-if="isDepartmentManager" class="truncate text-sm text-gray-700" :title="item.claimantName">
              <PersonName :name="item.claimantName" :emoji="item.claimantEmoji || rowEmoji(item)" />
            </span>
            <div>
              <span
                class="inline-flex max-w-full truncate rounded-full px-2.5 py-1 text-xs font-medium"
                :class="deptPills[toneIndex(item.departmentName, deptPills.length)]"
                :title="item.departmentName"
              >
                {{ item.departmentName }}
              </span>
            </div>
            <span class="text-sm text-gray-600">{{ formatExpenseDate(item.expenseDate) }}</span>
            <span class="text-sm text-gray-600">{{ formatCreated(item.createdAtUtc) }}</span>
            <span class="text-sm font-medium text-[#1e2430]">{{ formatZar(item.amount) }}</span>
            <div>
              <span class="inline-flex rounded-full px-2.5 py-1 text-xs font-medium" :class="statusMeta(item.status).className">
                {{ statusMeta(item.status).label }}
              </span>
            </div>
            <div class="flex justify-end">
              <NuxtLink
                :to="`/requests/${item.id}`"
                class="inline-flex h-8 items-center rounded-lg border border-[#e5e7eb] bg-white px-3 text-xs font-medium text-gray-500 hover:bg-gray-50"
                @click.stop
              >
                {{ item.status === 'Draft' ? 'Edit' : 'View' }}
              </NuxtLink>
            </div>
          </div>
        </div>
      </div>
    </div>
  </PortalShell>
</template>

<style scoped>
.request-grid {
  display: grid;
  grid-template-columns: minmax(200px, 1.7fr) minmax(140px, 1.15fr) minmax(120px, 0.95fr) minmax(120px, 0.95fr) 108px 110px 88px;
  align-items: center;
  column-gap: 12px;
}

.request-grid.with-requester {
  grid-template-columns: minmax(180px, 1.5fr) minmax(140px, 1fr) minmax(120px, 0.9fr) minmax(110px, 0.85fr) minmax(110px, 0.85fr) 108px 110px 88px;
}
</style>
