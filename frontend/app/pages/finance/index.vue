<script setup lang="ts">
import { formatZar } from '~/utils/format'
import { FOODBEV_CHART_COLORS } from '~/utils/foodbev-colors'

definePageMeta({ layout: 'finance' })

type DeptStat = {
  departmentId: string
  departmentName: string
  requestCount: number
  disbursedAmount: number
  inPipelineAmount: number
}

type Overview = {
  year: number
  month: number
  float: { capacity: number; balance: number; reserved: number; available: number }
  totalDisbursedInMonth: number
  totalRequestsInMonth: number
  departments: DeptStat[]
  topDepartmentByRequests: DeptStat | null
}

const api = useApi()
const { me } = useMe()

const now = new Date()
const selectedMonth = ref(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)

const monthQuery = computed(() => {
  const [y, m] = selectedMonth.value.split('-').map(Number)
  return { year: y, month: m }
})

type UploadDoc = {
  id: string
  fileName: string
  cashRequestId: string
  claimReference: string
  claimReason: string
  claimantName: string
}

const { data: claimDocuments } = await useAuthedAsyncData('finance-claim-documents', () =>
  api<UploadDoc[]>('/api/uploads/finance').catch(() => [] as UploadDoc[])
)

const recentDocuments = computed(() => (claimDocuments.value ?? []).slice(0, 5))

const { data: overview, refresh, pending } = await useAuthedAsyncData(
  'finance-overview',
  () => api<Overview>(`/api/finance/overview?year=${monthQuery.value.year}&month=${monthQuery.value.month}`),
  { watch: [selectedMonth] }
)

const monthLabel = computed(() => {
  const [y, m] = selectedMonth.value.split('-').map(Number)
  return new Date(y, m - 1, 1).toLocaleDateString('en-ZA', { month: 'long', year: 'numeric' })
})

const disbursedTotal = computed(() =>
  overview.value?.departments.reduce((s, d) => s + d.disbursedAmount, 0) ?? 0
)

const donutSegments = computed(() => {
  const depts = overview.value?.departments.filter((d) => d.disbursedAmount > 0) ?? []
  const total = disbursedTotal.value || 1
  const colors = [...FOODBEV_CHART_COLORS]
  let angle = 0
  return depts.map((d, i) => {
    const pct = (d.disbursedAmount / total) * 100
    const start = angle
    angle += pct
    return {
      ...d,
      pct,
      color: colors[i % colors.length],
      gradient: `${start}% ${angle}%`
    }
  })
})

const donutStyle = computed(() => {
  if (!donutSegments.value.length) {
    return { background: 'var(--fb-gauge-track)' }
  }
  const stops = donutSegments.value.flatMap((s, i, arr) => {
    const prev = arr.slice(0, i).reduce((sum, x) => sum + x.pct, 0)
    const end = prev + s.pct
    return [`${s.color} ${prev}% ${end}%`]
  })
  return { background: `conic-gradient(${stops.join(', ')})` }
})

const greeting = computed(() => {
  const hour = new Date().getHours()
  if (hour < 12) return 'Good morning'
  if (hour < 17) return 'Good afternoon'
  return 'Good evening'
})
</script>

<template>
  <div class="p-6 lg:p-8">
    <header class="mb-8 flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 class="text-2xl font-semibold text-slate-900">
          {{ greeting }}<span v-if="me">, {{ me.displayName.split(' ')[0] }}</span> 👋
        </h1>
        <p class="mt-1 text-sm text-slate-600">Petty cash overview for your organisation.</p>
      </div>
      <label class="text-sm text-slate-600">
        Period
        <input
          v-model="selectedMonth"
          type="month"
          class="ml-2 rounded-lg border border-foodbev-mist bg-white px-3 py-2 text-slate-900 focus:border-foodbev-blue focus:outline-none focus:ring-1 focus:ring-foodbev-blue"
          @change="refresh()"
        />
      </label>
    </header>

    <div v-if="pending && !overview" class="text-slate-500">Loading overview…</div>

    <template v-else-if="overview">
      <div class="mb-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <article class="card-foodbev p-5">
          <p class="text-sm text-slate-500">Hard cash on hand</p>
          <p class="mt-1 text-2xl font-semibold text-slate-900">{{ formatZar(overview.float.balance) }}</p>
          <p class="mt-1 text-xs text-foodbev-blue">Physical float balance</p>
        </article>
        <article class="card-foodbev p-5">
          <p class="text-sm text-slate-500">Available to allocate</p>
          <p class="mt-1 text-2xl font-semibold text-foodbev-sky">{{ formatZar(overview.float.available) }}</p>
          <p class="mt-1 text-xs text-slate-500">After reservations (max {{ formatZar(overview.float.capacity) }})</p>
        </article>
        <article class="card-foodbev p-5">
          <p class="text-sm text-slate-500">Disbursed in {{ monthLabel }}</p>
          <p class="mt-1 text-2xl font-semibold text-slate-900">{{ formatZar(overview.totalDisbursedInMonth) }}</p>
          <p class="mt-1 text-xs text-slate-500">Cash paid out this month</p>
        </article>
        <article class="card-foodbev p-5">
          <p class="text-sm text-slate-500">Requests in {{ monthLabel }}</p>
          <p class="mt-1 text-2xl font-semibold text-slate-900">{{ overview.totalRequestsInMonth }}</p>
          <p v-if="overview.topDepartmentByRequests" class="mt-1 text-xs text-slate-500">
            Most active: <span class="font-medium text-slate-800">{{ overview.topDepartmentByRequests.departmentName }}</span>
            ({{ overview.topDepartmentByRequests.requestCount }})
          </p>
          <p v-else class="mt-1 text-xs text-slate-500">No submissions this month yet</p>
        </article>
      </div>

      <div class="mb-8 grid gap-6 lg:grid-cols-2">
          <section class="card-foodbev p-6">
          <h2 class="mb-1 text-lg font-semibold text-slate-900">Spend by department</h2>
          <p class="mb-6 text-sm text-slate-500">Disbursed amounts in {{ monthLabel }}</p>
          <div class="flex flex-col items-center gap-6 sm:flex-row sm:items-start">
            <div
              class="relative h-40 w-40 shrink-0 rounded-full"
              :style="donutStyle"
            >
              <div class="absolute inset-4 flex flex-col items-center justify-center rounded-full bg-white text-center">
                <span class="text-xs text-slate-500">Total</span>
                <span class="text-sm font-semibold">{{ formatZar(disbursedTotal) }}</span>
              </div>
            </div>
            <ul class="min-w-0 flex-1 space-y-2 text-sm">
              <li
                v-for="seg in donutSegments"
                :key="seg.departmentId"
                class="flex items-center justify-between gap-2"
              >
                <span class="flex items-center gap-2 truncate">
                  <span class="h-2.5 w-2.5 shrink-0 rounded-full" :style="{ background: seg.color }" />
                  {{ seg.departmentName }}
                </span>
                <span class="shrink-0 text-slate-600">{{ formatZar(seg.disbursedAmount) }} ({{ seg.pct.toFixed(0) }}%)</span>
              </li>
              <li v-if="!donutSegments.length" class="text-slate-500">No disbursements this month.</li>
            </ul>
          </div>
        </section>

          <section class="card-foodbev p-6">
          <h2 class="mb-1 text-lg font-semibold text-slate-900">Requests by department</h2>
          <p class="mb-4 text-sm text-slate-500">Submitted in {{ monthLabel }} — who requested most</p>
          <ul class="space-y-4">
            <li v-for="dept in overview.departments" :key="dept.departmentId">
              <div class="mb-1 flex justify-between text-sm">
                <span class="font-medium text-slate-800">{{ dept.departmentName }}</span>
                <span class="text-slate-600">{{ dept.requestCount }} request(s)</span>
              </div>
              <div class="h-2 overflow-hidden rounded-full bg-foodbev-mist">
                <div
                  class="h-full rounded-full bg-foodbev-sky transition-all"
                  :style="{
                    width: `${overview.totalRequestsInMonth ? (dept.requestCount / overview.totalRequestsInMonth) * 100 : 0}%`
                  }"
                />
              </div>
              <p class="mt-1 text-xs text-slate-500">
                Disbursed {{ formatZar(dept.disbursedAmount) }}
                · In pipeline {{ formatZar(dept.inPipelineAmount) }}
              </p>
            </li>
            <li v-if="!overview.departments.length" class="text-sm text-slate-500">No department activity this month.</li>
          </ul>
        </section>
      </div>

      <section class="card-foodbev mb-8 p-6">
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 class="text-lg font-semibold text-slate-900">Claim documents</h2>
            <p class="text-sm text-slate-500">Files employees uploaded against a claim.</p>
          </div>
          <NuxtLink to="/finance/documents" class="text-sm font-medium text-foodbev-blue">View all</NuxtLink>
        </div>
        <ul v-if="recentDocuments.length" class="divide-y divide-foodbev-mist">
          <li v-for="file in recentDocuments" :key="file.id" class="flex flex-wrap items-center justify-between gap-2 py-3">
            <div>
              <p class="text-sm font-medium text-slate-800">{{ file.fileName }}</p>
              <p class="text-xs text-slate-500">
                {{ file.claimReference || 'Not linked to a claim' }}
                <span v-if="file.claimReason"> · {{ file.claimReason }}</span>
                <span v-if="file.claimantName"> · {{ file.claimantName }}</span>
              </p>
            </div>
            <NuxtLink
              v-if="file.cashRequestId && file.cashRequestId !== '00000000-0000-0000-0000-000000000000'"
              :to="`/requests/${file.cashRequestId}`"
              class="text-xs font-medium text-foodbev-blue"
            >
              Open claim
            </NuxtLink>
          </li>
        </ul>
        <p v-else class="text-sm text-slate-500">No claim documents yet.</p>
      </section>

          <section class="card-foodbev p-6">
        <h2 class="mb-4 text-lg font-semibold text-slate-900">Float snapshot</h2>
        <div class="grid gap-4 sm:grid-cols-3">
          <div>
            <p class="text-sm text-slate-500">Reserved (awaiting payout)</p>
            <p class="text-xl font-semibold">{{ formatZar(overview.float.reserved) }}</p>
          </div>
          <div>
            <p class="text-sm text-slate-500">Capacity</p>
            <p class="text-xl font-semibold">{{ formatZar(overview.float.capacity) }}</p>
          </div>
          <div>
            <p class="text-sm text-slate-500">Utilisation</p>
            <p class="text-xl font-semibold">
              {{ overview.float.capacity ? Math.round((overview.float.reserved / overview.float.capacity) * 100) : 0 }}%
            </p>
            <div class="mt-2 h-2 overflow-hidden rounded-full bg-foodbev-mist">
              <div
                class="h-full rounded-full bg-foodbev-blue"
                :style="{ width: `${Math.min(100, overview.float.capacity ? (overview.float.reserved / overview.float.capacity) * 100 : 0)}%` }"
              />
            </div>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>
