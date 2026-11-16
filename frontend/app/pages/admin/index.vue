<script setup lang="ts">
import { formatZar } from '~/utils/format'

definePageMeta({ layout: 'finance' })

type Matrix = {
  id: string
  departmentId: string
  departmentName: string
  primaryApproverUserId: string
  primaryApproverName: string
  actingApproverUserId: string | null
  actingApproverName: string | null
}

const api = useApi()
const error = ref('')
const amount = ref(1000)
const note = ref('')

const { data: matrices, refresh } = await useAuthedAsyncData('approval-matrices', () => api<Matrix[]>('/api/approval-matrices'))

async function replenish() {
  error.value = ''
  try {
    await api('/api/float/replenishments', {
      method: 'POST',
      body: JSON.stringify({ amount: Number(amount.value), note: note.value })
    })
    note.value = ''
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not record this float top-up. Check the amount and try again.'
  }
}
</script>

<template>
  <div class="space-y-8 p-6 lg:p-8">
    <section>
      <h1 class="mb-4 text-2xl font-semibold">Admin</h1>
      <h2 class="mb-2 text-lg font-medium">Approval matrix</h2>
      <div class="card-foodbev overflow-hidden">
        <table class="min-w-full text-sm">
          <thead class="table-head-foodbev">
            <tr>
              <th class="px-4 py-2">Department</th>
              <th class="px-4 py-2">Primary</th>
              <th class="px-4 py-2">Acting</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="m in matrices" :key="m.id" class="border-t border-foodbev-mist">
              <td class="px-4 py-2">{{ m.departmentName }}</td>
              <td class="px-4 py-2">{{ m.primaryApproverName }}</td>
              <td class="px-4 py-2">{{ m.actingApproverName || '—' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="card-foodbev max-w-md p-4">
      <h2 class="mb-2 text-lg font-medium">Record replenishment</h2>
      <form class="space-y-3" @submit.prevent="replenish">
        <label class="block text-sm">
          Amount
          <input v-model.number="amount" type="number" min="0.01" step="0.01" class="input-foodbev" />
        </label>
        <label class="block text-sm">
          Note
          <input v-model="note" class="input-foodbev" />
        </label>
        <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
        <button type="submit" class="btn-foodbev">Replenish float</button>
      </form>
      <p class="mt-2 text-xs text-slate-500">Float capacity remains {{ formatZar(10000) }}.</p>
    </section>
  </div>
</template>
