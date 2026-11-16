<script setup lang="ts">
import { formatZar } from '~/utils/format'

useAppLayout()

const route = useRoute()
const api = useApi()
const { loadMe, me } = useMe()
const error = ref('')
const message = ref('')
const claimantSignature = ref<string | null>(null)

type ApprovalStep = {
  stage: string
  assignedApproverName: string
  onBehalfOfName: string | null
  delegateUnavailable: boolean
  decision: string
  comment: string | null
}

type Detail = {
  id: string
  reason: string
  amount: number
  status: string
  departmentName: string
  businessUnitName: string
  budgetCode: string
  expenseDate: string
  claimantName: string
  approvalTrail: ApprovalStep[]
}

const { data: request, error: loadError, refresh, pending } = await useAuthedAsyncData(
  `request-detail-${route.params.id}`,
  () => api<Detail>(`/api/requests/${route.params.id}`)
)

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Submitted: 'Awaiting department manager',
    PendingFinanceApproval: 'Awaiting Finance senior manager',
    PendingDisbursement: 'Approved — ready for disbursement',
    Disbursed: 'Cash disbursed'
  }
  return labels[status] ?? status
}

async function submitRequest() {
  error.value = ''
  message.value = ''
  await loadMe(true)
  if (me.value?.requiresDelegatedMailbox && !me.value.mailboxConnected) {
    error.value = 'Connect your Microsoft 365 mailbox first (Account → Connect mailbox) so notification mail can send as you.'
    return
  }
  if (!claimantSignature.value) {
    error.value = 'Sign the petty cash form before submitting.'
    return
  }
  try {
    await api(`/api/requests/${route.params.id}/submit`, {
      method: 'POST',
      body: JSON.stringify({ signatureImagePngBase64: claimantSignature.value })
    })
    message.value = 'Request and signed form submitted to your department senior manager.'
    await refresh()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not submit this petty cash request. Check the form and try again.'
  }
}

async function cancelRequest() {
  error.value = ''
  try {
    await api(`/api/requests/${route.params.id}/cancel`, { method: 'POST', body: '{}' })
    await refresh()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not cancel this petty cash request. Refresh the page and try again.'
  }
}
</script>

<template>
  <div v-if="pending && !request" class="text-slate-500">Loading request…</div>
  <div v-else-if="loadError || !request" class="card-foodbev max-w-lg space-y-3 p-6">
    <p class="font-medium text-foodbev-navy">This request could not be loaded.</p>
    <p class="text-sm text-slate-600">You may not have access, or the request does not exist.</p>
    <div class="flex flex-wrap gap-2">
      <NuxtLink to="/manager" class="btn-foodbev-outline text-sm">Back to department overview</NuxtLink>
      <NuxtLink :to="`/requests/${route.params.id}/form`" class="btn-foodbev text-sm">Open petty cash form</NuxtLink>
    </div>
  </div>
  <div v-else class="space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h1 class="text-2xl font-semibold">Request detail</h1>
      <NuxtLink v-if="request.status !== 'Draft'" :to="`/requests/${request.id}/form`" class="btn-foodbev-outline text-sm">
        View petty cash form
      </NuxtLink>
    </div>

    
    <div class="card-foodbev p-6">
      <p><span class="font-medium">Reason:</span> {{ request.reason }}</p>
      <p><span class="font-medium">Amount:</span> {{ formatZar(request.amount) }}</p>
      <p><span class="font-medium">Status:</span> {{ statusLabel(request.status) }}</p>
      <p><span class="font-medium">Department:</span> {{ request.departmentName }}</p>
      <p><span class="font-medium">Business unit:</span> {{ request.businessUnitName }}</p>
      <p><span class="font-medium">Budget code:</span> {{ request.budgetCode }}</p>

      <div v-if="request.approvalTrail?.length" class="mt-4 space-y-3">
        <p class="font-medium">Approval trail</p>
        <div
          v-for="(step, index) in request.approvalTrail"
          :key="index"
          class="rounded-lg bg-foodbev-ice p-4 text-sm"
        >
          <p class="font-medium">{{ step.stage === 'Department' ? 'Department senior manager' : 'Finance senior manager' }}</p>
          <p>Assigned to: {{ step.assignedApproverName }}</p>
          <p v-if="step.onBehalfOfName">Covering for: {{ step.onBehalfOfName }}</p>
          <p v-if="step.delegateUnavailable" class="text-foodbev-blue">Acting approver flagged unavailable.</p>
          <p>Decision: {{ step.decision }}</p>
          <p v-if="step.comment">Comment: {{ step.comment }}</p>
        </div>
      </div>

      <div v-if="request.status === 'Draft'" class="mt-6 space-y-4 border-t border-foodbev-mist pt-6">
        <h2 class="text-lg font-semibold text-foodbev-navy">Petty Cash Form — 2026</h2>
        <p class="text-sm text-slate-600">
          Complete your claim details above, then sign below. This matches the official FoodBev petty cash voucher sent to
          Finance after your senior manager approves.
        </p>
        <PettyCashFormDocument
          :form="{
            displayId: String(route.params.id).replace(/-/g, '').slice(0, 8).toUpperCase(),
            claimantName: request.claimantName || '—',
            departmentName: request.departmentName,
            businessUnitName: request.businessUnitName,
            budgetCode: request.budgetCode,
            amount: request.amount,
            reason: request.reason,
            expenseDate: request.expenseDate,
            claimantSignedAtUtc: null,
            claimantSignaturePngBase64: claimantSignature,
            departmentManagerSignedAtUtc: null,
            departmentManagerSignaturePngBase64: null,
            departmentManagerName: null,
            financeManagerSignedAtUtc: null,
            financeManagerSignaturePngBase64: null,
            financeManagerName: null
          }"
        />
        <SignaturePad v-model="claimantSignature" label="Claimant signature (required to submit)" />
      </div>

      <div class="mt-4 flex gap-2">
        <button v-if="request.status === 'Draft'" class="btn-foodbev" @click="submitRequest">Submit signed form</button>
        <button v-if="request.status === 'Submitted'" class="btn-foodbev-outline" @click="cancelRequest">Cancel</button>
      </div>
      <p v-if="message" class="mt-2 text-sm text-foodbev-blue">{{ message }}</p>
      <p v-if="error" class="mt-2 text-sm text-red-600">{{ error }}</p>
    </div>
  </div>
</template>
