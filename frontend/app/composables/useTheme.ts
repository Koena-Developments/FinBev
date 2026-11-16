const STORAGE_KEY = 'finbev-theme'

export function useTheme() {
  const isDark = useState('finbev-dark', () => false)

  function apply(dark: boolean) {
    isDark.value = dark
    if (!import.meta.client) return
    document.documentElement.classList.toggle('dark', dark)
    localStorage.setItem(STORAGE_KEY, dark ? 'dark' : 'light')
  }

  function toggle() {
    apply(!document.documentElement.classList.contains('dark'))
  }

  function init() {
    if (!import.meta.client) return
    const saved = localStorage.getItem(STORAGE_KEY)
    apply(saved === 'dark')
  }

  return { isDark, apply, toggle, init }
}
