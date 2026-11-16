<script setup lang="ts">
import type { MeResponse } from '~/composables/useMe'

defineProps<{ tone?: 'light' | 'dark' }>()

const { me } = useMe()
const api = useApi()
const open = ref(false)
const saving = ref(false)
const error = ref('')

async function save(emoji: string | null) {
  if (!me.value || saving.value) return
  saving.value = true
  error.value = ''
  try {
    me.value = await api<MeResponse>('/api/auth/me/emoji', {
      method: 'PUT',
      body: JSON.stringify({ emoji })
    })
    open.value = false
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'We could not save your emoji. Try again.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div>
    <button
      type="button"
      class="flex w-full items-center gap-3 rounded-2xl px-1 py-1.5 text-left"
      :class="tone === 'dark' ? 'hover:bg-white/10' : 'hover:bg-foodbev-paper'"
      :disabled="saving"
      @click="open = true"
    >
      <span
        class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-white text-[28px] shadow-sm"
        :class="me?.emoji ? 'emoji-font' : 'text-sm font-semibold text-foodbev-navy'"
      >
        {{ me?.emoji || (me?.displayName?.trim().charAt(0).toUpperCase() || '•') }}
      </span>
      <span class="min-w-0">
        <span class="block text-sm font-semibold" :class="tone === 'dark' ? 'text-white' : 'text-foodbev-navy'">
          {{ me?.emoji ? 'Your emoji' : 'Add your emoji' }}
        </span>
        <span class="block text-xs" :class="tone === 'dark' ? 'text-foodbev-ice' : 'text-slate-500'">
          {{ saving ? 'Saving…' : 'Shown beside your name' }}
        </span>
      </span>
    </button>
    <p v-if="error" class="px-1 pt-1 text-xs text-red-600">{{ error }}</p>
    <EmojiPicker v-if="open" :current="me?.emoji" @close="open = false" @select="save" />
  </div>
</template>
