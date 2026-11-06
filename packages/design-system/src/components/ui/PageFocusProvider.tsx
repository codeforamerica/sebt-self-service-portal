'use client'

import { createContext, useContext, useRef, type MutableRefObject, type ReactNode } from 'react'

export interface TitleOwner {
  title: string
}

export interface MountedScreen {
  screenKey: string
  element: HTMLElement | null
}

export interface PageFocusState {
  /** Identity of the last screen whose heading rendered; null until the first one after document load. */
  lastScreen: string | null
  /** Headings currently mounted, innermost last; lets a nested loading screen hand focus back. */
  mountedScreens: MountedScreen[]
  /** Headings currently claiming the document title, innermost last. */
  titleOwners: TitleOwner[]
  /** The title the framework set, restored when the last owner leaves. */
  frameworkTitle: string | null
}

const PageFocusContext = createContext<MutableRefObject<PageFocusState> | null>(null)

/**
 * Remembers which screen a visitor is on so a page heading can tell a navigation
 * from a re-render. Mount once per app shell, around the routed content.
 */
export function PageFocusProvider({ children }: { children: ReactNode }) {
  const state = useRef<PageFocusState>({
    lastScreen: null,
    mountedScreens: [],
    titleOwners: [],
    frameworkTitle: null
  })
  return <PageFocusContext.Provider value={state}>{children}</PageFocusContext.Provider>
}

export function usePageFocusState(): MutableRefObject<PageFocusState> | null {
  return useContext(PageFocusContext)
}
