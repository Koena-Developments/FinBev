<script setup lang="ts">
import { formatZar } from '~/utils/format'

definePageMeta({ layout: false })

useHead({
  title: 'Upload',
  link: [
    {
      rel: 'stylesheet',
      href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap'
    }
  ]
})

type Department = { id: string; name: string }
type Claim = {
  id: string
  reason: string
  amount: number
  status: string
  departmentName: string
}
type Saved = {
  id: string
  fileName: string
  contentType: string
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
type Row = {
  key: string
  id?: string
  name: string
  size: number
  progress: number
  status: 'uploading' | 'done' | 'error'
  error?: string
  claimReference?: string
  claimReason?: string
}

const api = useApi()
const route = useRoute()
const { token } = useAuth()
const { me, loadMe, isApprover, isAdmin } = useMe()
const { isFinanceAdmin } = usePersona()
await loadMe()

watchEffect(() => {
  if (isFinanceAdmin.value) setPageLayout('finance')
})

const isDepartmentManager = computed(() => isApprover.value && !isAdmin.value)
const { data: departments } = await useAuthedAsyncData('departments', () => {
  if (!isAdmin.value) return Promise.resolve([] as Department[])
  return api<Department[]>('/api/departments').catch(() => [] as Department[])
})
const sidebarDepartments = computed(() => {
  if (isAdmin.value) return departments.value ?? []
  if (!isDepartmentManager.value || !me.value?.departmentId) return []
  return [{ id: me.value.departmentId, name: me.value.departmentName }]
})
const { data: existing } = await useAuthedAsyncData('my-uploads', () =>
  api<Saved[]>('/api/uploads').catch(() => [] as Saved[])
)
const { data: claims } = await useAuthedAsyncData('my-requests', () => api<Claim[]>('/api/requests'))

const claimId = ref(typeof route.query.claim === 'string' ? route.query.claim : '')
const claimError = ref('')
const rows = ref<Row[]>([])
const dragDepth = ref(0)
const dragging = computed(() => dragDepth.value > 0)
const fileInput = ref<HTMLInputElement | null>(null)

watch(
  existing,
  (files) => {
    if (!files?.length) return
    const known = new Set(rows.value.map((row) => row.id).filter(Boolean))
    for (const file of files) {
      if (known.has(file.id)) continue
      rows.value.push({
        key: file.id,
        id: file.id,
        name: file.fileName,
        size: file.sizeBytes,
        progress: 100,
        status: 'done',
        claimReference: file.claimReference,
        claimReason: file.claimReason
      })
    }
  },
  { immediate: true }
)

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function blockBrowserDrop(event: DragEvent) {
  event.preventDefault()
}

onMounted(() => {
  window.addEventListener('dragover', blockBrowserDrop)
  window.addEventListener('drop', blockBrowserDrop)
})

onBeforeUnmount(() => {
  window.removeEventListener('dragover', blockBrowserDrop)
  window.removeEventListener('drop', blockBrowserDrop)
})

function claimRef(id: string) {
  return id.replace(/-/g, '').slice(0, 8).toUpperCase()
}

function claimLabel(claim: Claim) {
  return `${claimRef(claim.id)} · ${claim.reason} · ${formatZar(claim.amount)}`
}

function requireClaim() {
  if (claimId.value) {
    claimError.value = ''
    return true
  }
  claimError.value = 'Choose the claim this document belongs to.'
  return false
}

function browse() {
  if (!requireClaim()) return
  fileInput.value?.click()
}

function onDragEnter(event: DragEvent) {
  if (!event.dataTransfer?.types.includes('Files')) return
  dragDepth.value += 1
}

function onDragLeave() {
  dragDepth.value = Math.max(0, dragDepth.value - 1)
}

function onDrop(event: DragEvent) {
  dragDepth.value = 0
  if (!requireClaim()) return
  accept([...(event.dataTransfer?.files ?? [])])
}

function onPicked(event: Event) {
  const input = event.target as HTMLInputElement
  accept([...(input.files ?? [])])
  input.value = ''
}

function accept(files: File[]) {
  if (!requireClaim()) return
  const selected = claimId.value
  const claim = claims.value?.find((item) => item.id === selected)
  for (const file of files) {
    const row: Row = {
      key: crypto.randomUUID(),
      name: file.name,
      size: file.size,
      progress: 0,
      status: 'uploading',
      claimReference: claimRef(selected),
      claimReason: claim?.reason ?? ''
    }
    rows.value.unshift(row)
    void upload(row, file, selected)
  }
}

function animateToward(row: Row, target: number, ms: number) {
  const start = performance.now()
  const from = row.progress
  return new Promise<void>((resolve) => {
    function frame(now: number) {
      const amount = Math.min(1, (now - start) / ms)
      const eased = 1 - (1 - amount) ** 3
      row.progress = Math.max(row.progress, Math.round(from + (target - from) * eased))
      if (amount < 1 && row.status === 'uploading') requestAnimationFrame(frame)
      else resolve()
    }
    requestAnimationFrame(frame)
  })
}

async function upload(row: Row, file: File, requestId: string) {
  const visual = animateToward(row, 88, 720)
  try {
    const saved = await postFile(file, requestId, (percent) => {
      row.progress = Math.max(row.progress, Math.min(92, percent))
    })
    await visual
    row.progress = 100
    row.id = saved.id
    row.name = saved.fileName
    row.size = saved.sizeBytes
    row.claimReference = saved.claimReference
    row.claimReason = saved.claimReason
    row.status = 'done'
  } catch (error) {
    row.status = 'error'
    row.error = error instanceof Error ? error.message : 'We could not upload this file. Check the file and try again.'
  }
}

function postFile(file: File, requestId: string, onProgress: (percent: number) => void) {
  return new Promise<Saved>((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', '/api/uploads')
    xhr.withCredentials = true
    xhr.setRequestHeader('X-Requested-With', 'PettyCash')
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress(Math.round((event.loaded / event.total) * 100))
    }
    xhr.onload = () => {
      let body: { message?: string } = {}
      try {
        body = xhr.responseText ? JSON.parse(xhr.responseText) : {}
      } catch {
        body = {}
      }
      if (xhr.status >= 200 && xhr.status < 300) resolve(body as Saved)
      else if (xhr.status === 401) reject(new Error('Sign in again to upload files.'))
      else reject(new Error(body.message || 'We could not upload this file. Check the file and try again.'))
    }
    xhr.onerror = () => reject(new Error('We could not upload this file. Check your connection and try again.'))
    const body = new FormData()
    body.append('file', file)
    body.append('cashRequestId', requestId)
    xhr.send(body)
  })
}

async function linkClaim(row: Row) {
  if (!row.id || !requireClaim()) return
  row.error = ''
  try {
    const saved = await api<Saved>(`/api/uploads/${row.id}/claim`, {
      method: 'POST',
      body: JSON.stringify({ cashRequestId: claimId.value })
    })
    row.claimReference = saved.claimReference
    row.claimReason = saved.claimReason
  } catch (error) {
    row.error = error instanceof Error ? error.message : 'We could not attach this file to the claim. Choose the claim again and retry.'
  }
}

async function remove(row: Row) {
  if (row.id) {
    await api(`/api/uploads/${row.id}`, { method: 'DELETE' })
  }
  rows.value = rows.value.filter((item) => item.key !== row.key)
}

async function download(row: Row) {
  if (!row.id || !token.value) return
  const response = await fetch(`/api/uploads/${row.id}`, {
    credentials: 'include',
    headers: { 'X-Requested-With': 'PettyCash' }
  })
  if (!response.ok) return
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = row.name
  link.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <PortalShell
    :departments="sidebarDepartments"
    :show-departments="sidebarDepartments.length > 0"
    :departments-selectable="isAdmin"
    :bare="isFinanceAdmin"
  >
    <div class="mx-auto max-w-3xl">
      <div class="flex items-center gap-3">
        <span class="flex h-11 w-11 items-center justify-center rounded-full bg-[#eef0ff] text-[#5458f2]">
          <svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="1.8">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 16V6" />
            <path stroke-linecap="round" stroke-linejoin="round" d="m8 9.5 4-4 4 4" />
            <path stroke-linecap="round" d="M5 19h14" />
          </svg>
        </span>
        <div>
          <h1 class="text-[22px] font-semibold tracking-tight text-[#1e2430]">Upload</h1>
          <p class="text-sm text-[#98a2b3]">Attach a receipt or invoice to one of your claims</p>
        </div>
      </div>

      <label class="mt-7 block text-sm font-medium text-[#1e2430]">
        Which claim is this for?
        <select v-model="claimId" class="mt-2 h-11 w-full rounded-xl border border-[#e6e8ef] bg-white px-3 text-sm outline-none focus:border-[#c7c9f8]">
          <option value="">Select a claim</option>
          <option v-for="claim in claims ?? []" :key="claim.id" :value="claim.id">
            {{ claimLabel(claim) }}
          </option>
        </select>
      </label>
      <p v-if="claimError" class="mt-2 text-sm text-red-500">{{ claimError }}</p>
      <p v-else-if="route.query.claim && claimId" class="mt-2 text-sm text-[#5458f2]">
        Upload the receipt for this petty cash claim.
      </p>
      <p v-if="claims && !claims.length" class="mt-2 text-sm text-[#98a2b3]">
        You do not have a claim yet.
        <NuxtLink to="/requests/new" class="font-medium text-[#5458f2]">Add a request</NuxtLink>
        before uploading a document.
      </p>

      <div
        class="dropzone mt-7"
        :class="{ 'is-over': dragging }"
        @dragenter.prevent="onDragEnter"
        @dragover.prevent
        @dragleave="onDragLeave"
        @drop.prevent.stop="onDrop"
        @click="browse"
      >
        <div class="dropzone__icon" aria-hidden="true">
          <span class="dropzone__file" />
          <svg viewBox="0 0 24 24" class="h-8 w-8" fill="none" stroke="currentColor" stroke-width="1.7">
            <path stroke-linecap="round" stroke-linejoin="round" d="M7 18a4 4 0 0 1 .4-8 5.5 5.5 0 0 1 10.7-1.5A3.5 3.5 0 0 1 18 18" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 16V9" />
            <path stroke-linecap="round" stroke-linejoin="round" d="m9.2 11.2 2.8-2.8 2.8 2.8" />
          </svg>
        </div>
        <p class="dropzone__title">{{ dragging ? 'Release to upload' : 'Drag and drop files here' }}</p>
        <p class="mt-1 text-sm text-[#98a2b3]">or browse from this device</p>
        <button type="button" class="browse" @click.stop="browse">Browse files</button>
        <p class="mt-4 text-xs text-[#b0b7c3]">PDF, PNG, JPG, WEBP, or DOCX · up to 10 MB</p>
        <input
          ref="fileInput"
          type="file"
          class="sr-only"
          multiple
          accept=".pdf,.png,.jpg,.jpeg,.webp,.doc,.docx,application/pdf,image/png,image/jpeg,image/webp"
          @click.stop
          @change="onPicked"
        />
      </div>

      <ul v-if="rows.length" class="mt-5 space-y-3">
        <li v-for="row in rows" :key="row.key" class="file-row">
          <span class="file-badge" :data-kind="row.name.split('.').pop()?.toLowerCase()">
            {{ (row.name.split('.').pop() || 'file').slice(0, 4).toUpperCase() }}
          </span>
          <div class="min-w-0 flex-1">
            <div class="flex items-center justify-between gap-3">
              <button
                type="button"
                class="truncate text-left text-sm font-medium text-[#1e2430] disabled:cursor-default"
                :disabled="row.status !== 'done'"
                @click="download(row)"
              >
                {{ row.name }}
              </button>
              <span class="shrink-0 text-xs font-medium" :class="row.status === 'error' ? 'text-red-500' : 'text-[#98a2b3]'">
                {{ row.status === 'error' ? 'Not uploaded' : row.status === 'done' ? 'Done' : `${row.progress}%` }}
              </span>
            </div>
            <p class="mt-0.5 text-xs text-[#98a2b3]">
              {{ formatSize(row.size) }}
              <span v-if="row.claimReference"> · {{ row.claimReference }}<span v-if="row.claimReason"> · {{ row.claimReason }}</span></span>
              <span v-else-if="row.status === 'done'"> · Not linked to a claim</span>
            </p>
            <button
              v-if="row.status === 'done' && !row.claimReference"
              type="button"
              class="mt-1 text-xs font-semibold text-[#5458f2]"
              @click="linkClaim(row)"
            >
              Link to the claim selected above
            </button>
            <div class="mt-2 h-1.5 overflow-hidden rounded-full bg-[#eef1f6]">
              <div
                class="bar"
                :class="row.status === 'error' ? 'bg-red-400' : row.status === 'done' ? 'bg-[#22a06b]' : 'bg-[#5458f2]'"
                :style="{ width: `${row.status === 'error' ? 100 : row.progress}%` }"
              />
            </div>
            <p v-if="row.error" class="mt-1 text-xs text-red-500">{{ row.error }}</p>
          </div>
          <span v-if="row.status === 'done'" class="check" aria-hidden="true">
            <svg viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="2.4">
              <path stroke-linecap="round" stroke-linejoin="round" d="m5 12 4.2 4.2L19 7.5" />
            </svg>
          </span>
          <button type="button" class="remove" aria-label="Remove file" @click="remove(row)">
            <svg viewBox="0 0 24 24" class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" d="M6 6l12 12M18 6 6 18" />
            </svg>
          </button>
        </li>
      </ul>
    </div>
  </PortalShell>
</template>

<style scoped>
.dropzone {
  display: flex;
  flex-direction: column;
  align-items: center;
  border-radius: 28px;
  border: 1.5px dashed #d9deea;
  background: #fbfcff;
  padding: 3rem 1.5rem;
  text-align: center;
  transition: border-color 0.25s ease, background 0.25s ease, transform 0.25s ease, box-shadow 0.25s ease;
}

.dropzone.is-over {
  border-color: #5458f2;
  background: #eef0ff;
  transform: scale(1.015);
  box-shadow: 0 16px 40px rgba(84, 88, 242, 0.12);
}

.dropzone__icon {
  position: relative;
  display: flex;
  height: 4.5rem;
  width: 4.5rem;
  align-items: center;
  justify-content: center;
  border-radius: 1.25rem;
  background: white;
  color: #5458f2;
  box-shadow: 0 8px 24px rgba(30, 36, 48, 0.06);
  animation: float 2.8s ease-in-out infinite;
  transition: transform 0.35s cubic-bezier(0.2, 0.8, 0.2, 1);
}

.dropzone.is-over .dropzone__icon {
  animation: none;
  transform: translateY(-12px) scale(1.06);
}

.dropzone__file {
  position: absolute;
  top: -0.4rem;
  right: -0.35rem;
  height: 1.15rem;
  width: 0.9rem;
  border-radius: 0.2rem;
  background: #c7c9f8;
  opacity: 0;
  transform: translateY(8px);
}

.dropzone.is-over .dropzone__file {
  animation: drop-in 0.45s ease forwards;
}

.dropzone__title {
  margin-top: 1.25rem;
  font-size: 1.05rem;
  font-weight: 600;
  color: #1e2430;
}

.browse {
  margin-top: 1rem;
  height: 2.5rem;
  border-radius: 0.85rem;
  background: #5458f2;
  padding: 0 1.1rem;
  font-size: 0.875rem;
  font-weight: 600;
  color: white;
  transition: background 0.2s ease;
}

.browse:hover {
  background: #4548e0;
}

.file-row {
  display: flex;
  align-items: center;
  gap: 0.9rem;
  border-radius: 1.1rem;
  border: 1px solid #eef0f4;
  background: white;
  padding: 0.9rem 1rem;
  animation: file-in 0.35s ease;
}

.file-badge {
  display: flex;
  height: 2.5rem;
  width: 2.5rem;
  shrink: 0;
  align-items: center;
  justify-content: center;
  border-radius: 0.8rem;
  background: #eef0ff;
  font-size: 0.62rem;
  font-weight: 700;
  letter-spacing: 0.02em;
  color: #5458f2;
}

.file-badge[data-kind='pdf'] {
  background: #fdecee;
  color: #e11d48;
}

.file-badge[data-kind='png'],
.file-badge[data-kind='jpg'],
.file-badge[data-kind='jpeg'],
.file-badge[data-kind='webp'] {
  background: #e7f8ef;
  color: #15966a;
}

.bar {
  height: 100%;
  border-radius: 999px;
  transition: width 0.25s ease;
}

.check {
  display: flex;
  height: 1.75rem;
  width: 1.75rem;
  shrink: 0;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  background: #e7f8ef;
  color: #15966a;
  animation: pop 0.35s ease;
}

.remove {
  display: flex;
  height: 2rem;
  width: 2rem;
  shrink: 0;
  align-items: center;
  justify-content: center;
  border-radius: 0.7rem;
  color: #98a2b3;
}

.remove:hover {
  background: #f6f7fb;
  color: #e11d48;
}

:global(html.dark .dropzone) {
  border-color: rgba(158, 197, 239, 0.28);
  background: #142640;
}

:global(html.dark .dropzone.is-over) {
  border-color: #5b9fd4;
  background: #1c3358;
}

:global(html.dark .dropzone__icon),
:global(html.dark .file-row) {
  border-color: rgba(158, 197, 239, 0.22);
  background: #1c3358;
  color: #c5ddf7;
}

:global(html.dark .dropzone__title) {
  color: #e8f0fb;
}

:global(html.dark .browse) {
  background: #3d6eb5;
}

:global(html.dark .browse:hover) {
  background: #1a4080;
}

:global(html.dark .file-badge) {
  background: rgba(77, 143, 209, 0.22);
  color: #c5ddf7;
}

:global(html.dark .file-badge[data-kind='pdf']) {
  background: rgba(225, 29, 72, 0.18);
  color: #fecdd3;
}

:global(html.dark .file-badge[data-kind='png']),
:global(html.dark .file-badge[data-kind='jpg']),
:global(html.dark .file-badge[data-kind='jpeg']),
:global(html.dark .file-badge[data-kind='webp']),
:global(html.dark .check) {
  background: rgba(21, 150, 106, 0.18);
  color: #bbf7d0;
}

:global(html.dark .remove) {
  color: #9eb0cc;
}

:global(html.dark .remove:hover) {
  background: rgba(225, 29, 72, 0.16);
  color: #fecdd3;
}

@keyframes float {
  0%,
  100% { transform: translateY(0); }
  50% { transform: translateY(-6px); }
}

@keyframes drop-in {
  to { opacity: 1; transform: translateY(0); }
}

@keyframes file-in {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: none; }
}

@keyframes pop {
  0% { transform: scale(0.4); }
  70% { transform: scale(1.15); }
  100% { transform: scale(1); }
}
</style>
