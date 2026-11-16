/** Drop any JWT left in browser storage and confirm the HttpOnly session cookie. */
export default defineNuxtPlugin(async () => {
  await useAuth().ensureSession()
})
