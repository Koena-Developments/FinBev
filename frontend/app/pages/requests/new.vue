<script setup lang="ts">
useAppLayout()

type Department = { id: string; name: string }
type BusinessUnit = { id: string; name: string; departmentId: string }
type BudgetCode = { id: string; code: string; description: string; businessUnitId: string }

const api = useApi()
const { me, loadMe } = useMe()
const businessUnits = ref<BusinessUnit[]>([])
const budgetCodes = ref<BudgetCode[]>([])
const error = ref('')

const form = reactive({
  departmentId: '',
  businessUnitId: '',
  budgetCodeId: '',
  expenseDate: new Date().toISOString().slice(0, 10),
  reason: '',
  amount: 0
})

await loadMe(true)
if (me.value?.departmentId) {
  form.departmentId = me.value.departmentId
  businessUnits.value = await api<BusinessUnit[]>(`/api/business-units?departmentId=${form.departmentId}`)
}

const departmentName = computed(() => me.value?.departmentName ?? '')
const claimantName = computed(() => me.value?.displayName ?? '')

watch(() => form.businessUnitId, async (id) => {
  form.budgetCodeId = ''
  budgetCodes.value = id ? await api<BudgetCode[]>(`/api/budget-codes?businessUnitId=${id}`) : []
})

const previewForm = computed(() => ({
  displayId: 'DRAFT',
  claimantName: claimantName.value,
  departmentName: departmentName.value,
  businessUnitName: businessUnits.value.find((b) => b.id === form.businessUnitId)?.name ?? '',
  budgetCode: budgetCodes.value.find((c) => c.id === form.budgetCodeId)?.code ?? '',
  amount: Number(form.amount) || 0,
  reason: form.reason,
  expenseDate: form.expenseDate,
  claimantSignedAtUtc: null,
  claimantSignaturePngBase64: null,
  departmentManagerSignedAtUtc: null,
  departmentManagerSignaturePngBase64: null,
  departmentManagerName: null,
  financeManagerSignedAtUtc: null,
  financeManagerSignaturePngBase64: null,
  financeManagerName: null
}))

async function saveDraft() {
  error.value = ''
  if (!form.departmentId) {
    error.value = 'Your profile has no department assigned. Contact HR or IT.'
    return
  }
  try {
    const created = await api<{ id: string }>('/api/requests', {
      method: 'POST',
      body: JSON.stringify({
        departmentId: form.departmentId,
        businessUnitId: form.businessUnitId,
        budgetCodeId: form.budgetCodeId,
        expenseDate: form.expenseDate,
        reason: form.reason,
        amount: Number(form.amount)
      })
    })
    await navigateTo(`/requests/${created.id}`)
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not save this petty cash request. Check the details and try again.'
  }
}
</script>

<template>
  <div class="mx-auto max-w-[900px] space-y-6 pb-10">
    <div>
      <h1 class="text-2xl font-bold text-foodbev-navy">New petty cash request</h1>
      <p class="mt-1 text-sm text-slate-600">
        Complete the fields below. Your department is fixed to your profile ({{ departmentName || 'loading…' }}). The preview matches
        the official 2026 petty cash form.
      </p>
    </div>

    <div class="card-foodbev space-y-4 p-6">
      <label class="block text-sm">
        Department
        <input :value="departmentName" class="input-foodbev bg-foodbev-ice" readonly disabled />
      </label>
      <label class="block text-sm">
        Business unit (cost centre on form)
        <select v-model="form.businessUnitId" class="input-foodbev" required>
          <option value="" disabled>Select business unit</option>
          <option v-for="b in businessUnits" :key="b.id" :value="b.id">{{ b.name }}</option>
        </select>
      </label>
      <label class="block text-sm">
        Budget code
        <select v-model="form.budgetCodeId" class="input-foodbev" required>
          <option value="" disabled>Select budget code</option>
          <option v-for="c in budgetCodes" :key="c.id" :value="c.id">{{ c.code }} — {{ c.description }}</option>
        </select>
      </label>
      <label class="block text-sm">
        Expense date
        <input v-model="form.expenseDate" type="date" class="input-foodbev" required />
      </label>
      <label class="block text-sm">
        Reason for claim
        <textarea v-model="form.reason" class="input-foodbev" rows="3" required />
      </label>
      <label class="block text-sm">
        Amount (max R2,000)
        <input v-model.number="form.amount" type="number" min="0.01" max="2000" step="0.01" class="input-foodbev" required />
      </label>
      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
      <button type="button" class="btn-foodbev" @click="saveDraft">Continue to sign &amp; submit</button>
    </div>

    <div>
      <p class="mb-2 text-sm font-medium text-foodbev-navy">Official form preview</p>
      <PettyCashFormDocument :form="previewForm" />
    </div>
  </div>
</template>
