<script setup lang="ts">
definePageMeta({ layout: false })

const route = useRoute()
const { me, loadMe } = useMe()
const { isFinanceAdmin, isDepartmentManager } = usePersona()
const lockedOut = computed(() => me.value?.mustChangePassword !== false)

watchEffect(() => {
  if (lockedOut.value) return
  if (isFinanceAdmin.value) setPageLayout('finance')
  else if (isDepartmentManager.value) setPageLayout('manager')
  else setPageLayout('default')
})

await loadMe()
const email = ref('')
const currentPassword = ref('')
const newPassword = ref('')
const error = ref('')
const { markSignedIn } = useAuth()
const api = useApi()

if (import.meta.client) {
  email.value = sessionStorage.getItem('pettycash_pending_email') || String(route.query.email || '')
}

async function submit() {
  error.value = ''
  try {
    const result = await api<{ success: boolean; token: string | null; message: string | null }>(
      '/api/auth/change-password',
      {
        method: 'POST',
        body: JSON.stringify({
          email: email.value.trim(),
          currentPassword: currentPassword.value,
          newPassword: newPassword.value
        })
      }
    )
    if (!result.success) {
      error.value = result.message || 'We could not change your password. Check your current password and try again.'
      return
    }
    if (import.meta.client) sessionStorage.removeItem('pettycash_pending_email')
    markSignedIn()
    await navigateTo('/')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not change your password. Check your current password and try again.'
  }
}
</script>

<template>
  <div :class="lockedOut ? 'relative flex min-h-screen flex-col items-center justify-center bg-foodbev-paper px-4 py-10' : 'mx-auto w-full max-w-md'">
    <div v-if="lockedOut" class="absolute right-4 top-4">
      <ThemeToggle />
    </div>
    <img
      v-if="lockedOut"
      src="/images/foodbev-logo.png"
      alt="FoodBev Manufacturing SETA"
      class="brand-logo mb-8 h-auto max-h-20 w-full max-w-[200px] object-contain"
      width="200"
      height="93"
    />
    <div class="w-full max-w-md card-foodbev p-6">
    <h1 class="mb-2 text-xl font-semibold text-foodbev-navy">Change password</h1>
    <p class="mb-4 text-sm text-slate-600">You must set a new password before using the portal.</p>
    <form class="space-y-4" @submit.prevent="submit">
      <label class="block text-sm">
        Email
        <input v-model="email" type="email" autocomplete="username" class="input-foodbev" required />
      </label>
      <label class="block text-sm">
        Temporary password
        <input v-model="currentPassword" type="password" class="input-foodbev" required />
      </label>
      <label class="block text-sm">
        New password
        <input v-model="newPassword" type="password" class="input-foodbev" required />
      </label>
      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
      <button type="submit" class="btn-foodbev-lg w-full">Update password</button>
    </form>
    </div>
  </div>
</template>
