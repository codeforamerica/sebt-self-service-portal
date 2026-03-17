/**
 * Vendor-agnostic, privacy-aware data layer.
 *
 * Acts as a single source of truth for page, user, and event metadata.
 * Supports scoped access, emits DOM CustomEvents for loose coupling to
 * analytics integrations, and follows the W3C CEDDL recommendations.
 *
 * @see https://www.w3.org/2013/12/ceddl-201312.pdf
 */

export interface DataLayerEvent {
  eventName: string
  eventData: Record<string, unknown>
  timeStamp: number
  scope: string[]
}

interface SetterNode {
  set: (path: string, value: unknown, scope?: string | string[]) => void
  [key: string]: unknown
}

export interface DataLayerRoot {
  page: SetterNode & {
    category: SetterNode
    attribute: SetterNode
  }
  user: SetterNode & {
    profile: SetterNode
  }
  event: DataLayerEvent[]
  privacy: { accessCategories: string[] }
  initialized: boolean
  get: (path: string, scope?: string, defaultValue?: unknown) => unknown
  trackEvent: (eventName: string, eventData?: Record<string, unknown>) => void
  eventTypes: Record<string, string>
}

function getPath(obj: Record<string, unknown>, path: string, defaultValue?: unknown): unknown {
  if (!obj || !path) return defaultValue

  const parts = path.split('.')
  let current: unknown = obj

  for (const part of parts) {
    if (current == null || typeof current !== 'object') {
      return defaultValue
    }
    current = (current as Record<string, unknown>)[part]
  }

  return current === undefined ? defaultValue : current
}

const FORBIDDEN_KEYS = new Set(['__proto__', 'constructor', 'prototype'])

function setPath(obj: Record<string, unknown>, path: string, value: unknown): void {
  if (!obj || !path) return

  const parts = path.split('.')

  // Prevent prototype pollution via dangerous path segments
  if (parts.some((segment) => FORBIDDEN_KEYS.has(segment))) {
    return
  }

  let current: Record<string, unknown> = obj

  for (let i = 0; i < parts.length - 1; i++) {
    const key = parts[i]!
    if (!current[key] || typeof current[key] !== 'object') {
      current[key] = {}
    }
    current = current[key] as Record<string, unknown>
  }

  current[parts[parts.length - 1]!] = value
}

// ── Core implementation ──

export class DataLayer {
  private readonly _root: string
  private readonly _data: Record<string, unknown>
  private readonly _scopes: Map<string, string[]>

  constructor(root: string, bootstrap?: { text?: string }) {
    this._root = root
    this._scopes = new Map()

    // Initialize canonical structure
    this._data = {
      page: { category: {}, attribute: {} },
      user: { profile: {} },
      event: [],
      privacy: { accessCategories: [] },
      initialized: false
    }

    // Apply bootstrap data if provided — supplemental, so fail gracefully
    if (bootstrap?.text) {
      try {
        const parsed = JSON.parse(bootstrap.text) as Record<string, unknown>
        this._deepMerge(this._data, parsed)
      } catch {
        // Malformed bootstrap JSON should not prevent initialization
      }
    }

    // Re-enforce canonical shape in case bootstrap overwrote structural nodes
    // with incompatible types (e.g., page: "oops" instead of an object)
    this._ensureCanonicalShape()

    // Bind public API onto the data structure
    this._bindApi()

    // Expose on window
    ;(window as Record<string, unknown>)[root] = this._data

    // Mark initialized and emit event
    this._data.initialized = true
    this._emit('DataLayer:Initialized', { rootElement: root })
  }

  // ── Public API binding ──

  private _bindApi(): void {
    const data = this._data
    const page = data.page as Record<string, unknown>
    const pageCategory = (page as Record<string, unknown>).category as Record<string, unknown>
    const pageAttribute = (page as Record<string, unknown>).attribute as Record<string, unknown>
    const user = data.user as Record<string, unknown>
    const userProfile = (user as Record<string, unknown>).profile as Record<string, unknown>

    // Root-level methods
    data.get = (path: string, scope?: string, defaultValue?: unknown): unknown => {
      return this._getElement(path, scope, defaultValue)
    }

    data.trackEvent = (eventName: string, eventData?: Record<string, unknown>): void => {
      this._trackEvent(eventName, eventData)
    }

    data.eventTypes = {
      INITIALIZED: 'DataLayer:Initialized',
      PAGE_ELEMENT_SET: `${this._root}:PageElementSet`,
      PAGE_ATTRIBUTE_SET: `${this._root}:PageAttributeSet`,
      PAGE_CATEGORY_SET: `${this._root}:PageCategorySet`,
      USER_ELEMENT_SET: `${this._root}:UserElementSet`,
      USER_PROFILE_SET: `${this._root}:UserProfileSet`,
      EVENT_TRACKED: `${this._root}:EventTracked`
    }

    // page.set, page.category.set, page.attribute.set
    page.set = this._createSetter('page', `${this._root}:PageElementSet`, false)
    pageCategory.set = this._createSetter('page.category', `${this._root}:PageCategorySet`, false)
    pageAttribute.set = this._createSetter(
      'page.attribute',
      `${this._root}:PageAttributeSet`,
      false
    )

    // user.set, user.profile.set — enforce 'default' scope
    user.set = this._createSetter('user', `${this._root}:UserElementSet`, true)
    userProfile.set = this._createSetter('user.profile', `${this._root}:UserProfileSet`, true)
  }

  private _createSetter(
    basePath: string,
    eventName: string,
    enforceDefaultScope: boolean
  ): (path: string, value: unknown, scope?: string | string[]) => void {
    return (path: string, value: unknown, scope?: string | string[]): void => {
      const fullPath = `${basePath}.${path}`

      // Store value in the data structure
      setPath(this._data, fullPath, value)

      // Resolve scope
      const scopes = this._resolveWriteScope(scope, enforceDefaultScope)
      if (scopes.length > 0) {
        this._scopes.set(fullPath, scopes)
      }

      // Emit DOM event
      this._emit(eventName, { path: fullPath, value })
    }
  }

  // ── Read ──

  private _getElement(path: string, scope?: string, defaultValue?: unknown): unknown {
    const value = getPath(this._data, path, undefined)
    if (value === undefined) return defaultValue

    // If no scope requested, return the value directly
    if (!scope) return value

    // Check if scope grants access
    if (this._hasAccess(path, scope)) {
      return value
    }

    return defaultValue
  }

  private _hasAccess(path: string, scope: string): boolean {
    // Walk the path from specific to general, looking for scope restrictions
    const parts = path.split('.')
    for (let i = parts.length; i > 0; i--) {
      const checkPath = parts.slice(0, i).join('.')
      const elementScope = this._scopes.get(checkPath)
      if (elementScope) {
        return elementScope.includes(scope)
      }
    }

    // No scope found anywhere in the hierarchy — publicly readable
    return true
  }

  // ── Event tracking ──

  private _trackEvent(eventName: string, eventData?: Record<string, unknown>): void {
    const eventObj: DataLayerEvent = {
      eventName,
      eventData: eventData ?? {},
      timeStamp: Date.now(),
      scope: []
    }

    const eventArray = this._data.event as DataLayerEvent[]
    eventArray.push(eventObj)

    this._emit(`${this._root}:EventTracked`, {
      eventName,
      eventData: eventObj.eventData,
      timeStamp: eventObj.timeStamp,
      scope: eventObj.scope
    })
  }

  // ── Helpers ──

  private _resolveWriteScope(
    scope: string | string[] | undefined,
    enforceDefault: boolean
  ): string[] {
    const scopes: string[] = []

    if (typeof scope === 'string') {
      scopes.push(scope)
    } else if (Array.isArray(scope)) {
      scopes.push(...scope)
    }

    if (enforceDefault && !scopes.includes('default')) {
      scopes.push('default')
    }

    return scopes
  }

  private _emit(eventName: string, detail?: Record<string, unknown>): void {
    document.dispatchEvent(
      new CustomEvent(eventName, {
        bubbles: true,
        detail
      })
    )
  }

  private _ensureCanonicalShape(): void {
    const ensureObject = (parent: Record<string, unknown>, key: string): void => {
      if (!parent[key] || typeof parent[key] !== 'object' || Array.isArray(parent[key])) {
        parent[key] = {}
      }
    }

    ensureObject(this._data, 'page')
    const page = this._data.page as Record<string, unknown>
    ensureObject(page, 'category')
    ensureObject(page, 'attribute')
    ensureObject(this._data, 'user')
    const user = this._data.user as Record<string, unknown>
    ensureObject(user, 'profile')

    if (!Array.isArray(this._data.event)) {
      this._data.event = []
    }
  }

  private _deepMerge(target: Record<string, unknown>, source: Record<string, unknown>): void {
    for (const key of Object.keys(source)) {
      if (key === '__proto__' || key === 'constructor') continue
      const sourceVal = source[key]
      const targetVal = target[key]

      if (
        sourceVal &&
        typeof sourceVal === 'object' &&
        !Array.isArray(sourceVal) &&
        targetVal &&
        typeof targetVal === 'object' &&
        !Array.isArray(targetVal)
      ) {
        this._deepMerge(targetVal as Record<string, unknown>, sourceVal as Record<string, unknown>)
      } else {
        target[key] = sourceVal
      }
    }
  }
}

declare global {
  interface Window {
    digitalData?: DataLayerRoot
  }
}
