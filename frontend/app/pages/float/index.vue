<script setup lang="ts">
import { formatZar } from '~/utils/format'

definePageMeta({ layout: 'finance' })

type FloatSummary = {
  capacity: number
  balance: number
  reserved: number
  available: number
}

type PendingItem = {
  id: string
  reason: string
  amount: number
  departmentName: string
  claimantName?: string
}

const api = useApi()
const error = ref('')

const { data: summary, refresh: refreshSummary } = await useAuthedAsyncData('float-summary', () => api<FloatSummary>('/api/float'))
const { data: pending, refresh: refreshPending } = await useAuthedAsyncData('pending-disbursement', () =>
  api<PendingItem[]>('/api/requests/pending-disbursement')
)

async function disburse(id: string) {
  error.value = ''
  try {
    await api(`/api/requests/${id}/disburse`, { method: 'POST', body: '{}' })
    await Promise.all([refreshSummary(), refreshPending()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not pay out this petty cash request. Refresh the page and try again.'
  }
}
</script>

<template>
  <div class="space-y-8 p-6 lg:p-8">
    <div>
      <h1 class="mb-4 text-2xl font-semibold">Float status</h1>
      <div v-if="summary" class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div class="card-foodbev p-4">
          <p class="text-sm text-slate-500">Capacity</p>
          <p class="text-xl font-semibold">{{ formatZar(summary.capacity) }}</p>
        </div>
        <div class="card-foodbev p-4">
          <p class="text-sm text-slate-500">Balance</p>
          <p class="text-xl font-semibold">{{ formatZar(summary.balance) }}</p>
        </div>
        <div class="card-foodbev p-4">
          <p class="text-sm text-slate-500">Reserved</p>
          <p class="text-xl font-semibold">{{ formatZar(summary.reserved) }}</p>
        </div>
        <div class="card-foodbev p-4">
          <p class="text-sm text-slate-500">Available</p>
          <p class="text-xl font-semibold text-foodbev-sky">{{ formatZar(summary.available) }}</p>
        </div>
      </div>
    </div>

    <div>
      <h2 class="mb-3 text-xl font-semibold">Ready to disburse</h2>
      <p class="mb-3 text-sm text-slate-600">Requests that have department and Finance senior manager approval.</p>
      <p v-if="error" class="mb-2 text-sm text-red-600">{{ error }}</p>
      <div class="space-y-3">
        <article v-for="item in pending" :key="item.id" class="card-foodbev flex flex-wrap items-center justify-between gap-3 p-4">
          <div>
            <p class="font-medium">{{ item.reason }}</p>
            <p class="text-sm text-slate-600">{{ item.departmentName }} · {{ formatZar(item.amount) }}</p>
          </div>
          <div class="flex gap-2">
            <NuxtLink :to="`/requests/${item.id}`" class="px-3 py-1.5 text-sm link-foodbev">View</NuxtLink>
            <button type="button" class="btn-foodbev px-3 py-1.5" @click="disburse(item.id)">Disburse cash</button>
          </div>
        </article>
        <p v-if="!pending?.length" class="text-slate-500">No requests waiting for disbursement.</p>
      </div>
    </div>
  </div>
</template>
