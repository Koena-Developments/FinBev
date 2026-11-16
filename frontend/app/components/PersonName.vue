<script setup lang="ts">
defineProps<{ name?: string | null; emoji?: string | null }>()
</script>

<template>
  <span class="inline-flex min-w-0 max-w-full items-center gap-1.5">
    <span v-if="emoji" class="emoji-font shrink-0 text-[1.05em]" aria-hidden="true">{{ emoji }}</span>
    <span class="truncate">{{ name || '—' }}</span>
  </span>
</template>
