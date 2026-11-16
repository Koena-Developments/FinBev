<script setup lang="ts">
const { ensureSession } = useAuth()
const { loadMe } = useMe()
const { homePath } = usePersona()

if (import.meta.client) {
  const signedIn = await ensureSession()
  if (!signedIn) {
    await navigateTo('/login')
  } else {
    await loadMe(true)
    await navigateTo(homePath())
  }
}
</script>
