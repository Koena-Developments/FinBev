export default defineEventHandler(() => {
  const tenantId = String(process.env.NUXT_MICROSOFT_TENANT_ID || '').trim()
  const clientId = String(process.env.NUXT_MICROSOFT_CLIENT_ID || '').trim()
  return {
    enabled: Boolean(tenantId && clientId),
    tenantId: tenantId || null
  }
})