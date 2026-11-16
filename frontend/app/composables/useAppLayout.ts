/** Pick finance / manager / default shell from roles. */
export function useAppLayout() {
  const { isFinanceAdmin, isDepartmentManager } = usePersona()

  watchEffect(() => {
    if (isFinanceAdmin.value) {
      setPageLayout('finance')
    } else if (isDepartmentManager.value) {
      setPageLayout('manager')
    } else {
      setPageLayout('default')
    }
  })
}
