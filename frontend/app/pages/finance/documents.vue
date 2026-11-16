<script setup lang="ts">
import { formatZar } from '~/utils/format'

definePageMeta({ layout: 'finance' })

type UploadDoc = {
  id: string
  fileName: string
  sizeBytes: number
  createdAtUtc: string
  cashRequestId: string
  claimReference: string
  claimReason: string
  claimantName: string
  departmentName: string
  amount: number
  status: string
}

const api = useApi()
const { token } = useAuth()

const { data: documents, pending, error } = await useAuthedAsyncData('finance-claim-documents', () =>
  api<UploadDoc[]>('/api/uploads/finance')
)

const groups = computed(() => {
  const byClaim = new Map<string, { claim: UploadDoc; files: UploadDoc[] }>()
  for (const file of documents.value ?? []) {
    const current = byClaim.get(file.cashRequestId)
    if (current) {
      current.files.push(file)
    } else {
      byClaim.set(file.cashRequestId, { claim: file, files: [file] })
    }
  }
  return [...byClaim.values()]
})

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Draft: 'Draft',
    Submitted: 'Awaiting department manager',
    PendingFinanceApproval: 'Awaiting you',
    PendingDisbursement: 'Approved — ready to disburse',
    Disbursed: 'Cash disbursed',
    Approved: 'Approved',
    Rejected: 'Rejected',
    Cancelled: 'Cancelled'
  }
  return labels[status] ?? status
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatWhen(value: string) {
  return new Date(value).toLocaleDateString('en-ZA', {
    day: 'numeric',
    month: 'short',
    year: 'numeric'
  })
}

async function download(file: UploadDoc) {
  if (!token.value) return
  const response = await fetch(`/api/uploads/${file.id}`, {
    credentials: 'include',
    headers: { 'X-Requested-With': 'PettyCash' }
  })
  if (!response.ok) return
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = file.fileName
  link.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="p-6 lg:p-8">
    <header class="mb-6">
      <h1 class="text-2xl font-semibold text-slate-900">Claim documents</h1>
      <p class="mt-1 text-sm text-slate-600">
        Receipts and invoices employees attached to a petty cash claim.
      </p>
    </header>

    <p v-if="pending && !documents" class="text-sm text-slate-500">Loading documents…</p>
    <p v-else-if="error" class="text-sm text-red-600">{{ error.message }}</p>
    <p v-else-if="!groups.length" class="card-foodbev p-8 text-center text-sm text-slate-500">
      No documents have been uploaded yet.
    </p>

    <div v-else class="space-y-4">
      <section v-for="group in groups" :key="group.claim.cashRequestId" class="card-foodbev overflow-hidden">
        <div class="flex flex-wrap items-start justify-between gap-3 border-b border-foodbev-mist px-5 py-4">
          <div>
            <p v-if="group.claim.claimReference" class="font-mono text-xs font-semibold text-foodbev-navy">{{ group.claim.claimReference }}</p>
            <h2 class="mt-1 text-base font-semibold text-slate-900">{{ group.claim.claimReason || (group.claim.claimReference ? 'Claim' : 'Not linked to a claim') }}</h2>
            <p class="mt-1 text-sm text-slate-600">
              {{ group.claim.claimantName || 'Employee' }}
              <span v-if="group.claim.departmentName"> · {{ group.claim.departmentName }}</span>
              <span v-if="group.claim.amount"> · {{ formatZar(group.claim.amount) }}</span>
            </p>
          </div>
          <span v-if="group.claim.status" class="rounded-full bg-foodbev-mist px-3 py-1 text-xs font-medium text-foodbev-navy">
            {{ statusLabel(group.claim.status) }}
          </span>
        </div>
        <ul class="divide-y divide-foodbev-mist">
          <li v-for="file in group.files" :key="file.id" class="flex flex-wrap items-center justify-between gap-3 px-5 py-3">
            <div>
              <p class="text-sm font-medium text-slate-800">{{ file.fileName }}</p>
              <p class="text-xs text-slate-500">{{ formatSize(file.sizeBytes) }} · {{ formatWhen(file.createdAtUtc) }}</p>
            </div>
            <button type="button" class="btn-foodbev px-3 py-1.5 text-xs" @click="download(file)">
              Download
            </button>
          </li>
        </ul>
      </section>
    </div>
  </div>
</template>
