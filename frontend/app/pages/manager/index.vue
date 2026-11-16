<script setup lang="ts">
import { formatZar } from '~/utils/format'

definePageMeta({ layout: false })

useHead({ title: 'Department overview' })

type Overview = {
  year: number
  month: number
  managedDepartmentNames: string[]
  pendingMyApprovalCount: number
  totalRequestsInMonth: number
  totalRequestedAmountInMonth: number
  totalDisbursedInMonth: number
  statusBreakdown: { label: string; count: number; amount: number }[]
  dailyTrend: { day: number; requestCount: number; amount: number }[]
  recentRequests: {
    id: string
    reason: string
    amount: number
    status: string
    claimantName: string
    claimantEmoji?: string | null
    submittedAtUtc: string | null
    expenseDate?: string
    departmentName?: string
    createdAtUtc?: string
  }[]
}

const api = useApi()
const { me, loadMe } = useMe()
await loadMe()

const now = new Date()
const selectedMonth = ref(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
const period = computed(() => {
  const [y, m] = selectedMonth.value.split('-').map(Number)
  return { year: y, month: m }
})

const { data: overview, refresh, pending } = await useAuthedAsyncData(
  'manager-overview',
  () => api<Overview>(`/api/manager/overview?year=${period.value.year}&month=${period.value.month}`),
  { watch: [selectedMonth] }
)

const monthLabel = computed(() => {
  const [y, m] = selectedMonth.value.split('-').map(Number)
  return new Date(y, m - 1, 1).toLocaleDateString('en-ZA', { month: 'long', year: 'numeric' })
})

const maxTrend = computed(() =>
  Math.max(1, ...(overview.value?.dailyTrend.map((t) => t.requestCount) ?? [1]))
)

const gaugePercent = computed(() => {
  if (!overview.value?.totalRequestsInMonth) return 0
  const done = overview.value.statusBreakdown
    .filter((s) => s.label === 'Disbursed' || s.label === 'With Finance' || s.label === 'Ready to pay out')
    .reduce((n, s) => n + s.count, 0)
  return Math.min(100, Math.round((done / overview.value.totalRequestsInMonth) * 100))
})

const deptLabel = computed(() => overview.value?.managedDepartmentNames.join(', ') ?? me.value?.departmentName ?? 'Your department')

const sidebarDepartments = computed(() => {
  const names = overview.value?.managedDepartmentNames ?? []
  if (names.length) return names.map((name) => ({ id: name, name }))
  if (me.value?.departmentId) return [{ id: me.value.departmentId, name: me.value.departmentName }]
  return []
})

const historyTotal = computed(() =>
  overview.value?.recentRequests.reduce((sum, row) => sum + row.amount, 0) ?? 0
)

function formatWhen(value: string | null | undefined) {
  if (!value) return '—'
  return new Date(value).toLocaleDateString('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' })
}
</script>

<template>
  <PortalShell
    :departments="sidebarDepartments"
    :show-departments="sidebarDepartments.length > 0"
    :departments-selectable="false"
  >
  <div>
    <div class="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-foodbev-navy">Department overview</h1>
        <p class="mt-1 text-sm text-slate-600">{{ deptLabel }}</p>
      </div>
      <label class="text-sm text-slate-600">
        Period
        <input
          v-model="selectedMonth"
          type="month"
          class="ml-2 rounded-xl border border-foodbev-mist bg-white px-3 py-2"
          @change="refresh()"
        />
      </label>
    </div>

    <p v-if="pending && !overview" class="text-slate-500">Loading…</p>

    <template v-else-if="overview">
      <div class="grid gap-6 lg:grid-cols-12">
        <!-- Left column -->
        <div class="space-y-6 lg:col-span-4">
          <article class="card-foodbev rounded-3xl p-6">
            <h2 class="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-500">Awaiting you</h2>
            <div class="relative mx-auto mb-4 h-36 w-36">
              <div
                class="absolute inset-0 rounded-full"
                :style="{
                  background: `conic-gradient(#4d8fd1 ${gaugePercent * 1.8}deg, var(--fb-gauge-track) 0deg)`
                }"
              />
              <div class="absolute inset-3 flex flex-col items-center justify-center rounded-full bg-white text-center">
                <span class="text-3xl font-bold text-foodbev-navy">{{ overview.pendingMyApprovalCount }}</span>
                <span class="text-xs text-slate-500">to sign</span>
              </div>
            </div>
            <p class="text-center text-sm text-slate-600">
              {{ overview.pendingMyApprovalCount ? 'Review pending approvals' : 'Inbox clear for now' }}
            </p>
            <NuxtLink
              to="/approvals"
              class="mt-4 block rounded-xl bg-foodbev-sky py-2.5 text-center text-sm font-semibold text-white hover:opacity-90"
            >
              Open approvals
            </NuxtLink>
          </article>

          <article class="card-foodbev rounded-3xl p-6">
            <h2 class="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-500">Status mix</h2>
            <ul class="space-y-3 text-sm">
              <li v-for="row in overview.statusBreakdown" :key="row.label" class="flex justify-between gap-2">
                <span class="text-slate-700">{{ row.label }}</span>
                <span class="font-medium text-foodbev-navy">{{ row.count }} · {{ formatZar(row.amount) }}</span>
              </li>
              <li v-if="!overview.statusBreakdown.length" class="text-slate-500">No requests this month.</li>
            </ul>
          </article>
        </div>

        <!-- Main chart area -->
        <div class="space-y-6 lg:col-span-8">
          <article class="card-foodbev rounded-3xl p-6">
            <div class="mb-6 flex flex-wrap items-start justify-between gap-4">
              <div>
                <p class="text-sm text-slate-500">Petty cash requests · {{ monthLabel }}</p>
                <p class="text-3xl font-bold text-foodbev-navy">{{ overview.totalRequestsInMonth }}</p>
                <p class="text-sm text-slate-600">{{ formatZar(overview.totalRequestedAmountInMonth) }} requested</p>
              </div>
              <div class="rounded-2xl bg-foodbev-mist px-4 py-3 text-right">
                <p class="text-xs text-slate-500">Disbursed</p>
                <p class="text-lg font-bold text-foodbev-sky">{{ formatZar(overview.totalDisbursedInMonth) }}</p>
              </div>
            </div>
            <div class="flex h-40 items-end gap-1 border-b border-foodbev-mist pb-2">
              <div
                v-for="point in overview.dailyTrend"
                :key="point.day"
                class="group flex-1 rounded-t-md bg-foodbev-navy/80 transition hover:bg-foodbev-sky"
                :style="{ height: `${(point.requestCount / maxTrend) * 100}%`, minHeight: point.requestCount ? '4px' : '2px' }"
                :title="`Day ${point.day}: ${point.requestCount} request(s)`"
              />
            </div>
            <p class="mt-2 text-xs text-slate-400">Daily submissions across the month</p>
          </article>

          <article class="card-foodbev rounded-3xl p-6">
            <div class="mb-4 flex flex-wrap items-end justify-between gap-3">
              <div>
                <h2 class="text-lg font-semibold text-foodbev-navy">Request history</h2>
                <p class="mt-1 text-sm text-slate-600">Who requested petty cash in {{ deptLabel }}, and how much.</p>
              </div>
              <p v-if="overview.recentRequests.length" class="text-sm font-semibold text-foodbev-navy">
                {{ overview.recentRequests.length }} requests · {{ formatZar(historyTotal) }}
              </p>
            </div>
            <div class="max-h-[28rem] overflow-auto">
              <table class="min-w-full text-sm">
                <thead class="sticky top-0 bg-white">
                  <tr class="border-b border-foodbev-mist text-left text-slate-500">
                    <th class="pb-2 pr-4 font-medium">Requested by</th>
                    <th class="pb-2 pr-4 font-medium">Reason</th>
                    <th class="pb-2 pr-4 font-medium">Amount</th>
                    <th class="pb-2 pr-4 font-medium">Requested</th>
                    <th class="pb-2 font-medium">Status</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="row in overview.recentRequests" :key="row.id" class="border-b border-foodbev-mist/60">
                    <td class="py-3 pr-4 font-medium text-foodbev-navy"><PersonName :name="row.claimantName" :emoji="row.claimantEmoji" /></td>
                    <td class="py-3 pr-4">
                      <NuxtLink :to="`/requests/${row.id}/form`" class="text-foodbev-sky hover:underline">{{ row.reason }}</NuxtLink>
                    </td>
                    <td class="py-3 pr-4">{{ formatZar(row.amount) }}</td>
                    <td class="py-3 pr-4 text-slate-600">{{ formatWhen(row.submittedAtUtc) }}</td>
                    <td class="py-3 text-slate-600">{{ row.status }}</td>
                  </tr>
                  <tr v-if="!overview.recentRequests.length">
                    <td colspan="5" class="py-6 text-slate-500">No one in this department has requested petty cash yet.</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </article>
        </div>
      </div>
    </template>
  </div>
  </PortalShell>
</template>
