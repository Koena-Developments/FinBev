<script setup lang="ts">
import { formatZar } from '~/utils/format'

useAppLayout()
const { isDepartmentManager } = usePersona()

type QueueItem = {
  requestId: string
  displayId: string
  reason: string
  amount: number
  departmentName: string
  claimantName: string
  claimantEmoji?: string | null
  displayApprovalStatus: 'Approved' | 'Pending' | 'Rejected'
  datePaid: string | null
  financeApprovedAtUtc: string | null
  pendingTaskId: string | null
  pendingStage: string | null
}

type DepartmentGroup = {
  departmentName: string
  items: QueueItem[]
  summaryStatus: 'Approved' | 'Pending' | 'Rejected' | 'Mixed'
}

type FormCopy = {
  requestId: string
  displayId: string
  reason: string
  amount: number
  departmentName: string
  claimantName: string
  claimantEmoji?: string | null
  releasedAtUtc: string
}

const api = useApi()
const { loadMe, me } = useMe()
const comment = ref('')
const error = ref('')
const approverSignature = ref<string | null>(null)
const expanded = ref<Record<string, boolean>>({})

const { data: financeCopies } = await useAuthedAsyncData('finance-form-copies', () =>
  api<FormCopy[]>('/api/petty-cash-form/finance-copies').catch(() => [])
)

type InboxItem = {
  taskId: string
  requestId: string
  reason: string
  amount: number
  status: string
  departmentName: string
  claimantName: string
  claimantEmoji?: string | null
  stage: string
}

const { data: inbox, refresh: refreshInbox } = await useAuthedAsyncData('approvals-inbox', () =>
  api<InboxItem[]>('/api/approvals/inbox')
)

const { data: queue, refresh: refreshQueue } = await useAuthedAsyncData('approvals-queue', () =>
  api<QueueItem[]>('/api/approvals/queue')
)

async function refresh() {
  await Promise.all([refreshInbox(), refreshQueue()])
}

const groups = computed<DepartmentGroup[]>(() => {
  const byDept = new Map<string, QueueItem[]>()
  for (const item of queue.value ?? []) {
    const list = byDept.get(item.departmentName) ?? []
    list.push(item)
    byDept.set(item.departmentName, list)
  }

  return [...byDept.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([departmentName, items]) => ({
      departmentName,
      items,
      summaryStatus: summarizeGroup(items)
    }))
})

function summarizeGroup(items: QueueItem[]): DepartmentGroup['summaryStatus'] {
  const statuses = new Set(items.map((i) => i.displayApprovalStatus))
  if (statuses.has('Rejected')) return 'Rejected'
  if (statuses.size === 1 && statuses.has('Approved')) return 'Approved'
  if (statuses.has('Pending')) return 'Pending'
  return 'Mixed'
}

function toggleGroup(name: string) {
  expanded.value[name] = !expanded.value[name]
}

function isExpanded(name: string) {
  return expanded.value[name] !== false
}

function formatTableDate(iso: string | null) {
  if (!iso) return '—'
  const d = new Date(iso)
  return d
    .toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
    .replace(/ /g, ' ')
    .toUpperCase()
}

function formatDatePaid(iso: string | null) {
  if (!iso) return '—'
  const [y, m, day] = iso.split('-').map(Number)
  const d = new Date(y, m - 1, day)
  return d
    .toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
    .replace(/ /g, ' ')
    .toUpperCase()
}

function statusPillClass(status: string) {
  if (status === 'Approved') return 'bg-sky-100 text-foodbev-blue'
  if (status === 'Rejected') return 'bg-red-100 text-red-700'
  return 'bg-amber-100 text-amber-800'
}

function statusDotClass(status: string) {
  if (status === 'Approved') return 'bg-foodbev-sky'
  if (status === 'Rejected') return 'bg-red-500'
  return 'bg-amber-500'
}

function signaturePayload(dataUrl: string | null): string | null {
  if (!dataUrl) return null
  const comma = dataUrl.indexOf(',')
  return comma >= 0 ? dataUrl.slice(comma + 1) : dataUrl
}

async function decide(taskId: string, approved: boolean, stage: string | null) {
  error.value = ''
  await loadMe(true)
  if (me.value?.requiresDelegatedMailbox && !me.value.mailboxConnected) {
    error.value = 'Connect your Microsoft 365 mailbox first (Account → Connect mailbox) so notification mail can send as you.'
    return
  }
  if (approved && stage === 'Department' && !approverSignature.value) {
    error.value = 'Sign the petty cash form as department senior manager before approving.'
    return
  }
  try {
    const path = approved ? 'approve' : 'reject'
    await api(`/api/approvals/${taskId}/${path}`, {
      method: 'POST',
      body: JSON.stringify({
        comment: comment.value,
        signatureImagePngBase64: approved && stage === 'Department' ? signaturePayload(approverSignature.value) : null
      })
    })
    comment.value = ''
    await refresh()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not save your approval or rejection. Refresh the page and try again.'
  }
}
</script>

<template>
  <div :class="isDepartmentManager ? 'mx-auto max-w-[1400px] p-4 lg:p-8' : 'p-4 lg:p-8'">
    <div class="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-foodbev-navy">Approvals</h1>
        <p class="mt-1 text-sm text-slate-600">
          Status shows <strong>Approved</strong> only after Finance senior manager sign-off; otherwise
          <strong>Pending</strong>.
        </p>
      </div>
      <label class="block text-sm text-slate-600">
        Comment (optional)
        <input v-model="comment" class="input-foodbev mt-1 min-w-[16rem]" placeholder="Add a note to your decision" />
      </label>
    </div>

    <div class="card-foodbev mb-6 max-w-xl p-4">
      <SignaturePad
        v-model="approverSignature"
        label="Department senior manager signature (required when you approve — forwarded to Finance with the requester’s signature)"
      />
    </div>

    <section v-if="financeCopies?.length" class="card-foodbev mb-6 overflow-hidden">
      <div class="border-b border-foodbev-mist bg-foodbev-mist/50 px-4 py-3">
        <h2 class="font-semibold text-foodbev-navy">Petty cash forms for Finance</h2>
        <p class="text-xs text-slate-600">
          Signed by requester and department senior manager — for Boitumelo Moripe, Busisiwe Ntuli, and Finance senior
          manager review.
        </p>
      </div>
      <ul class="divide-y divide-foodbev-mist">
        <li v-for="copy in financeCopies" :key="copy.requestId" class="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
          <div>
            <p class="font-mono text-xs text-foodbev-navy">{{ copy.displayId }}</p>
            <p class="font-medium">{{ copy.reason }}</p>
            <p class="text-sm text-slate-600">
              <PersonName :name="copy.claimantName" :emoji="copy.claimantEmoji" /> · {{ copy.departmentName }} · {{ formatZar(copy.amount) }}
            </p>
          </div>
          <NuxtLink :to="`/requests/${copy.requestId}/form`" class="btn-foodbev px-3 py-1.5 text-xs">Open form</NuxtLink>
        </li>
      </ul>
    </section>

    <p v-if="error" class="mb-4 text-sm text-red-600">{{ error }}</p>

    <section v-if="inbox?.length" class="card-foodbev mb-6 overflow-hidden border-2 border-foodbev-sky/40">
      <div class="border-b border-foodbev-mist bg-foodbev-sky/10 px-4 py-3">
        <h2 class="font-semibold text-foodbev-navy">Awaiting your signature</h2>
        <p class="text-xs text-slate-600">
          Open the signed petty cash form from the employee, add your signature below, then approve.
        </p>
      </div>
      <ul class="divide-y divide-foodbev-mist">
        <li
          v-for="item in inbox"
          :key="item.taskId"
          class="flex flex-wrap items-center justify-between gap-3 px-4 py-3"
        >
          <div>
            <p class="font-medium text-slate-800">{{ item.reason }}</p>
            <p class="text-sm text-slate-600">
              <PersonName :name="item.claimantName" :emoji="item.claimantEmoji" /> · {{ item.departmentName }} · {{ formatZar(item.amount) }}
            </p>
            <p class="text-[10px] uppercase tracking-wide text-slate-500">
              {{ item.stage === 'Finance' ? 'Finance senior manager' : 'Department senior manager' }}
            </p>
          </div>
          <div class="flex flex-wrap gap-2">
            <NuxtLink :to="`/requests/${item.requestId}/form`" class="btn-foodbev px-3 py-1.5 text-xs">
              Open signed form
            </NuxtLink>
            <button
              type="button"
              class="btn-foodbev px-3 py-1.5 text-xs"
              @click="decide(item.taskId, true, item.stage)"
            >
              Approve
            </button>
            <button
              type="button"
              class="btn-foodbev-outline px-3 py-1.5 text-xs"
              @click="decide(item.taskId, false, item.stage)"
            >
              Reject
            </button>
          </div>
        </li>
      </ul>
    </section>

    <div v-if="!groups.length" class="card-foodbev p-8 text-center text-slate-500">
      No requests in your approval queue.
    </div>

    <div v-else class="space-y-4">
      <section v-for="(group, index) in groups" :key="group.departmentName" class="card-foodbev overflow-hidden">
        <button
          type="button"
          class="flex w-full items-center gap-4 border-b border-foodbev-mist px-4 py-3 text-left transition hover:bg-foodbev-paper/80"
          @click="toggleGroup(group.departmentName)"
        >
          <span class="w-8 shrink-0 text-sm text-slate-500">{{ index + 1 }}</span>
          <span class="min-w-0 flex-1 font-semibold text-foodbev-navy">{{ group.departmentName }}</span>
          <span
            class="inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs font-medium"
            :class="statusPillClass(group.summaryStatus === 'Mixed' ? 'Pending' : group.summaryStatus)"
          >
            <span
              class="h-2 w-2 rounded-full"
              :class="statusDotClass(group.summaryStatus === 'Mixed' ? 'Pending' : group.summaryStatus)"
            />
            {{ group.summaryStatus === 'Mixed' ? 'Items require attention' : group.summaryStatus }}
          </span>
          <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-foodbev-mist text-foodbev-navy">
            {{ isExpanded(group.departmentName) ? '−' : '+' }}
          </span>
        </button>

        <div v-show="isExpanded(group.departmentName)" class="bg-white">
          <div class="flex items-center justify-between border-b border-foodbev-mist bg-foodbev-mist/50 px-4 py-2">
            <p class="text-sm font-medium text-foodbev-navy">{{ group.items.length }} request(s)</p>
          </div>

          <div class="overflow-x-auto">
            <table class="min-w-full text-sm">
              <thead class="table-head-foodbev">
                <tr>
                  <th class="px-4 py-3 font-semibold">ID</th>
                  <th class="px-4 py-3 font-semibold">Reason for request</th>
                  <th class="px-4 py-3 font-semibold">Date paid</th>
                  <th class="px-4 py-3 font-semibold">Approval status</th>
                  <th class="px-4 py-3 font-semibold">Finance approval date</th>
                  <th class="px-4 py-3 font-semibold">Amount</th>
                  <th class="px-4 py-3 font-semibold">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="item in group.items"
                  :key="item.requestId"
                  class="border-t border-foodbev-mist/60 hover:bg-foodbev-paper/50"
                >
                  <td class="px-4 py-3 font-mono text-xs font-semibold text-foodbev-navy">{{ item.displayId }}</td>
                  <td class="max-w-xs px-4 py-3">
                    <p class="font-medium text-slate-800">{{ item.reason }}</p>
                    <p class="mt-0.5 text-xs text-slate-500"><PersonName :name="item.claimantName" :emoji="item.claimantEmoji" /></p>
                  </td>
                  <td class="whitespace-nowrap px-4 py-3 text-slate-700">{{ formatDatePaid(item.datePaid) }}</td>
                  <td class="px-4 py-3">
                    <span
                      class="inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold"
                      :class="statusPillClass(item.displayApprovalStatus)"
                    >
                      {{ item.displayApprovalStatus }}
                    </span>
                  </td>
                  <td class="whitespace-nowrap px-4 py-3 text-slate-700">
                    {{ formatTableDate(item.financeApprovedAtUtc) }}
                  </td>
                  <td class="whitespace-nowrap px-4 py-3 font-medium">{{ formatZar(item.amount) }}</td>
                  <td class="px-4 py-3">
                    <div class="flex flex-wrap items-center gap-2">
                      <template v-if="item.pendingTaskId">
                        <button
                          type="button"
                          class="btn-foodbev px-2.5 py-1 text-xs"
                          @click="decide(item.pendingTaskId!, true, item.pendingStage)"
                        >
                          Approve
                        </button>
                        <button
                          type="button"
                          class="btn-foodbev-outline px-2.5 py-1 text-xs"
                          @click="decide(item.pendingTaskId!, false, item.pendingStage)"
                        >
                          Reject
                        </button>
                      </template>
                      <NuxtLink :to="`/requests/${item.requestId}/form`" class="text-xs link-foodbev">Signed form</NuxtLink>
                      <NuxtLink :to="`/requests/${item.requestId}`" class="text-xs link-foodbev">Details</NuxtLink>
                    </div>
                    <p v-if="item.pendingStage" class="mt-1 text-[10px] uppercase tracking-wide text-slate-500">
                      Your step: {{ item.pendingStage === 'Finance' ? 'Finance SM' : 'Department SM' }}
                    </p>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>
