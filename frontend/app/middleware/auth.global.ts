export default defineNuxtRouteMiddleware(async (to) => {
  const publicPaths = ['/login'] as const
  const isPublic = publicPaths.includes(to.path as typeof publicPaths[number])

  // The session cookie is on this site. The browser sends it, so auth runs in the browser.
  if (import.meta.server) {
    return
  }

  if (isPublic && to.path === '/login') {
    return
  }

  const { token, ensureSession, signOut } = useAuth()
  const signedIn = await ensureSession()

  if (isPublic) {
    return
  }

  if (!signedIn || !token.value) {
    return navigateTo('/login')
  }

  const { loadMe, hasRole } = useMe()
  const isDeptManager = hasRole('Approver') && !hasRole('Admin')

  try {
    const profile = await loadMe()
    if (!profile) {
      return navigateTo('/login')
    }
    if (to.path.startsWith('/manager') && !isDeptManager) {
      return navigateTo(hasRole('Admin') ? '/finance' : '/requests')
    }

    if (to.path.startsWith('/finance') && !hasRole('Admin')) {
      return navigateTo(isDeptManager ? '/manager' : '/requests')
    }
    if (to.path.startsWith('/admin') && !hasRole('Admin')) {
      return navigateTo('/requests')
    }
    if (to.path.startsWith('/float') && !hasRole('Admin')) {
      return navigateTo('/requests')
    }
    if (to.path.startsWith('/approvals') && !hasRole('Approver')) {
      return navigateTo('/requests')
    }
  } catch {
    await signOut()
  }
})
