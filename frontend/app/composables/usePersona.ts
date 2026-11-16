/** Finance admin (e.g. Mogopodi) vs department senior manager vs employee. */
export function usePersona() {
  const { isAdmin, isApprover } = useMe()

  const isFinanceAdmin = computed(() => isAdmin.value)
  const isDepartmentManager = computed(() => isApprover.value && !isAdmin.value)

  function homePath() {
    if (isFinanceAdmin.value) return '/finance'
    if (isDepartmentManager.value) return '/manager'
    return '/requests'
  }

  return { isFinanceAdmin, isDepartmentManager, homePath }
}
