<script setup lang="ts">
import type { PettyCashFormData } from '~/components/PettyCashFormDocument.vue'

definePageMeta({ ssr: false })

useAppLayout()

const route = useRoute()
const { token, ensureSession } = useAuth()
const api = useApi()

type ApiForm = PettyCashFormData & {
  requestId?: string
  status?: string
  releasedToFinanceCopies?: boolean
}

function normalizeExpenseDate(value: unknown): string {
  if (typeof value === 'string') return value
  if (value && typeof value === 'object' && 'year' in value && 'month' in value && 'day' in value) {
    const v = value as { year: number; month: number; day: number }
    return `${v.year}-${String(v.month).padStart(2, '0')}-${String(v.day).padStart(2, '0')}`
  }
  return ''
}

function toFormData(raw: Record<string, unknown>): PettyCashFormData {
  const pick = (camel: string, pascal: string) => raw[camel] ?? raw[pascal]
  return {
    displayId: String(pick('displayId', 'DisplayId') ?? ''),
    claimantName: String(pick('claimantName', 'ClaimantName') ?? ''),
    departmentName: String(pick('departmentName', 'DepartmentName') ?? ''),
    businessUnitName: String(pick('businessUnitName', 'BusinessUnitName') ?? ''),
    budgetCode: String(pick('budgetCode', 'BudgetCode') ?? ''),
    amount: Number(pick('amount', 'Amount') ?? 0),
    reason: String(pick('reason', 'Reason') ?? ''),
    expenseDate: normalizeExpenseDate(pick('expenseDate', 'ExpenseDate')),
    claimantSignedAtUtc: (pick('claimantSignedAtUtc', 'ClaimantSignedAtUtc') as string | null) ?? null,
    claimantSignaturePngBase64: (pick('claimantSignaturePngBase64', 'ClaimantSignaturePngBase64') as string | null) ?? null,
    departmentManagerSignedAtUtc: (pick('departmentManagerSignedAtUtc', 'DepartmentManagerSignedAtUtc') as string | null) ?? null,
    departmentManagerSignaturePngBase64: (pick('departmentManagerSignaturePngBase64', 'DepartmentManagerSignaturePngBase64') as string | null) ?? null,
    departmentManagerName: (pick('departmentManagerName', 'DepartmentManagerName') as string | null) ?? null,
    financeManagerSignedAtUtc: (pick('financeManagerSignedAtUtc', 'FinanceManagerSignedAtUtc') as string | null) ?? null,
    financeManagerSignaturePngBase64: (pick('financeManagerSignaturePngBase64', 'FinanceManagerSignaturePngBase64') as string | null) ?? null,
    financeManagerName: (pick('financeManagerName', 'FinanceManagerName') as string | null) ?? null
  }
}

const form = ref<PettyCashFormData | null>(null)
const loadError = ref('')
const pending = ref(true)

async function loadForm() {
  loadError.value = ''
  pending.value = true
  form.value = null
  await ensureSession()
  if (!token.value) {
    pending.value = false
    loadError.value = 'You are not signed in.'
    await navigateTo('/login')
    return
  }
  try {
    const raw = await api<ApiForm>(`/api/petty-cash-form/${route.params.id}`)
    form.value = toFormData(raw as unknown as Record<string, unknown>)
  } catch (e) {
    loadError.value = e instanceof Error ? e.message : 'We could not open this petty cash form. Refresh the page and try again.'
  } finally {
    pending.value = false
  }
}

onMounted(() => {
  void loadForm()
})

watch(
  () => route.params.id,
  () => {
    void loadForm()
  }
)

async function downloadDocx() {
  await ensureSession()
  const response = await fetch(
    `/api/petty-cash-form/${route.params.id}/document.docx`,
    { credentials: 'include', headers: { 'X-Requested-With': 'PettyCash' } }
  )
  if (!response.ok) {
    const body = await response.json().catch(() => ({}))
    throw new Error(body.message || 'We could not download the Word form. Try again, or use Print / Save as PDF.')
  }
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `PettyCash-${route.params.id}.docx`
  a.click()
  URL.revokeObjectURL(url)
}

function printForm() {
  window.print()
}
</script>

<template>
  <div class="space-y-4 p-4 lg:p-8 print:space-y-0 print:p-0">
    <div class="flex flex-wrap gap-2 print:hidden">
      <NuxtLink :to="`/requests/${route.params.id}`" class="btn-foodbev-outline">Back to request</NuxtLink>
      <button type="button" class="btn-foodbev" :disabled="!form" @click="printForm">Print / Save as PDF</button>
      <button type="button" class="btn-foodbev-outline" :disabled="!form" @click="downloadDocx">Download Word form</button>
    </div>

    <p v-if="pending" class="text-slate-500">Loading petty cash form…</p>

    <div v-else-if="loadError" class="card-foodbev max-w-lg space-y-3 p-6">
      <p class="font-medium text-foodbev-navy">This form could not be loaded.</p>
      <p class="text-sm text-slate-600">{{ loadError }}</p>
      <p class="text-sm text-slate-600">
        Confirm you are signed in, then try again.
      </p>
      <button type="button" class="btn-foodbev text-sm" @click="loadForm()">Try again</button>
    </div>

    <PettyCashFormDocument v-else-if="form" :form="form" />
  </div>
</template>

<style scoped>
@media print {
  :global(header),
  :global(nav),
  :global(.print\\:hidden) {
    display: none !important;
  }
}
</style>
