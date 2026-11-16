/**
 * The session cookie is sent by the browser, so authenticated calls stay on the client.
 */
export function useAuthedAsyncData<T>(
  key: string,
  fetcher: () => Promise<T>,
  options?: Parameters<typeof useAsyncData<T>>[2]
) {
  const { token, sessionId } = useAuth()

  return useAsyncData(
    key,
    () => {
      if (!token.value) {
        return Promise.resolve(null as T)
      }
      return fetcher()
    },
    { server: false, watch: [token, sessionId], ...options }
  )
}
