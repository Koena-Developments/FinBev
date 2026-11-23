export default defineEventHandler(() => {
  const config = useRuntimeConfig()
  const tenantId = String(config.microsoftTenantId || '').trim()
  const clientId = String(config.microsoftClientId || '').trim()
  return {
    enabled: Boolean(tenantId && clientId),
    tenantId: tenantId || null
  }
})