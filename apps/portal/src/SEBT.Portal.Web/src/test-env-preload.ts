/**
 * First Vitest setup file: no imports from `@/` so this runs before any module can load `env.ts`.
 * `createEnv()` requires NEXT_PUBLIC_STATE; workers may not inherit shell or Vitest `test.env` reliably.
 */
process.env.NEXT_PUBLIC_STATE ??= 'dc'

// Node.js 22+ exposes a native `localStorage` global, but it requires `--localstorage-file`
// to be pointed at a valid path — without it the object has no methods. Replace it with an
// in-memory implementation so tests that call localStorage.setItem / clear / getItem work.
if (typeof globalThis.localStorage?.clear !== 'function') {
  const store: Record<string, string> = {}
  Object.defineProperty(globalThis, 'localStorage', {
    configurable: true,
    value: {
      setItem(key: string, value: string) {
        store[key] = String(value)
      },
      getItem(key: string): string | null {
        return Object.prototype.hasOwnProperty.call(store, key) ? (store[key] as string) : null
      },
      removeItem(key: string) {
        delete store[key]
      },
      clear() {
        Object.keys(store).forEach((k) => delete store[k])
      },
      get length() {
        return Object.keys(store).length
      },
      key(index: number): string | null {
        return Object.keys(store)[index] ?? null
      }
    }
  })
}
