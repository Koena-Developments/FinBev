<script setup lang="ts">
import { staffEmojiGroups } from '~/data/staffEmojis'

const props = defineProps<{ current?: string | null }>()
const emit = defineEmits<{ select: [emoji: string | null]; close: [] }>()

const recentKey = 'finbev-recent-emojis'
const query = ref('')
const active = ref(staffEmojiGroups[0]?.id ?? 'smileys')
const recent = ref<string[]>([])

onMounted(() => {
  try {
    const stored = JSON.parse(localStorage.getItem(recentKey) || '[]')
    if (Array.isArray(stored)) {
      recent.value = stored.filter((item): item is string => typeof item === 'string').slice(0, 16)
    }
  } catch {
    recent.value = []
  }
})

const visible = computed(() => {
  const keyword = query.value.trim().toLowerCase()
  const source = keyword
    ? staffEmojiGroups.flatMap((group) => group.emojis)
    : staffEmojiGroups.find((group) => group.id === active.value)?.emojis ?? []
  const seen = new Set<string>()
  return source.filter((item) => {
    if (seen.has(item.emoji)) return false
    seen.add(item.emoji)
    return !keyword || item.name.includes(keyword) || item.emoji.includes(keyword)
  })
})

function remember(emoji: string) {
  const next = [emoji, ...recent.value.filter((item) => item !== emoji)].slice(0, 16)
  recent.value = next
  localStorage.setItem(recentKey, JSON.stringify(next))
}

function choose(emoji: string) {
  remember(emoji)
  emit('select', emoji)
}

function emojiName(emoji: string) {
  for (const group of staffEmojiGroups) {
    const match = group.emojis.find((item) => item.emoji === emoji)
    if (match) return match.name
  }
  return 'emoji'
}

function onKey(event: KeyboardEvent) {
  if (event.key === 'Escape') emit('close')
}
</script>

<template>
  <Teleport to="body">
    <div class="fixed inset-0 z-[80] flex items-end justify-center bg-slate-900/40 p-0 sm:items-center sm:p-6" @click.self="emit('close')" @keydown="onKey">
      <div
        class="flex max-h-[min(34rem,100dvh)] w-full max-w-[26rem] flex-col overflow-hidden rounded-t-[28px] border border-[var(--fb-border)] bg-[var(--fb-surface)] shadow-2xl sm:rounded-[28px]"
        role="dialog"
        aria-label="Choose an emoji"
        @click.stop
      >
        <div class="flex items-center justify-between px-4 pb-2 pt-4">
          <div>
            <p class="text-base font-semibold text-[var(--fb-text)]">Choose your emoji</p>
            <p class="text-xs text-[var(--fb-text-muted)]">This is how you appear to other people.</p>
          </div>
          <button type="button" class="rounded-full px-3 py-1 text-sm font-medium text-[var(--fb-text-muted)] hover:bg-[var(--fb-surface-2)]" @click="emit('close')">
            Close
          </button>
        </div>

        <div class="px-4 pb-3">
          <input
            v-model="query"
            type="search"
            placeholder="Search emoji"
            class="h-10 w-full rounded-xl border border-[var(--fb-border)] bg-[var(--fb-surface-2)] px-3 text-sm text-[var(--fb-text)] outline-none placeholder:text-[var(--fb-text-muted)] focus:border-foodbev-blue"
            autofocus
          />
        </div>

        <div v-if="!query && recent.length" class="px-3 pb-2">
          <p class="px-1 pb-1 text-[11px] font-semibold tracking-wide text-[var(--fb-text-muted)]">RECENT</p>
          <div class="flex gap-1 overflow-x-auto">
            <button
              v-for="emoji in recent"
              :key="emoji"
              type="button"
              class="emoji-font flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-[26px] hover:bg-[var(--fb-surface-2)]"
              :class="emoji === props.current ? 'bg-[var(--fb-surface-3)]' : ''"
              :aria-label="emojiName(emoji)"
              @click="choose(emoji)"
            >
              {{ emoji }}
            </button>
          </div>
        </div>

        <div class="min-h-0 flex-1 overflow-y-auto px-3 pb-2">
          <p v-if="!visible.length" class="px-2 py-8 text-center text-sm text-[var(--fb-text-muted)]">No emoji matches that search.</p>
          <div v-else class="grid grid-cols-8 gap-0.5">
            <button
              v-for="item in visible"
              :key="`${item.emoji}-${item.name}`"
              type="button"
              class="emoji-font flex h-11 items-center justify-center rounded-xl text-[28px] hover:bg-[var(--fb-surface-2)]"
              :class="item.emoji === props.current ? 'bg-[var(--fb-surface-3)] ring-2 ring-foodbev-sky' : ''"
              :aria-label="item.name"
              :title="item.name"
              @click="choose(item.emoji)"
            >
              {{ item.emoji }}
            </button>
          </div>
        </div>

        <div class="flex items-center gap-1 border-t border-[var(--fb-border)] bg-[var(--fb-surface-2)] px-2 py-2">
          <button
            type="button"
            class="rounded-full px-3 py-2 text-xs font-semibold text-[var(--fb-text-muted)] hover:bg-[var(--fb-surface)]"
            @click="emit('select', null)"
          >
            None
          </button>
          <div class="flex min-w-0 flex-1 justify-between">
            <button
              v-for="group in staffEmojiGroups"
              :key="group.id"
              type="button"
              class="emoji-font flex h-9 w-9 items-center justify-center rounded-full text-[20px]"
              :class="!query && active === group.id ? 'bg-[var(--fb-surface)] shadow-sm' : 'opacity-70 hover:opacity-100'"
              :aria-label="group.label"
              :title="group.label"
              @click="active = group.id; query = ''"
            >
              {{ group.icon }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>
