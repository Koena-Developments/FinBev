<script setup lang="ts">
defineProps<{
  tone?: 'surface' | 'on-gradient'
}>()

const { toggle } = useTheme()
</script>

<template>
  <button
    type="button"
    class="theme-toggle"
    :class="tone === 'on-gradient' ? 'theme-toggle--on-gradient' : ''"
    aria-label="Toggle colour theme"
    title="Toggle colour theme"
    @click.stop="toggle"
  >
    <svg class="theme-toggle__moon" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M16.5 13.2A6.2 6.2 0 0 1 10.8 7 6.4 6.4 0 1 0 16.5 13.2Z" />
    </svg>
    <svg class="theme-toggle__sun" viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="12" r="3.2" />
      <path d="M12 3.5v2.2M12 18.3v2.2M3.5 12h2.2M18.3 12h2.2M6 6l1.6 1.6M16.4 16.4 18 18M18 6l-1.6 1.6M7.6 16.4 6 18" />
    </svg>
  </button>
</template>
