'use client'

import { usePathname } from 'next/navigation'
import { useEffect, useRef, type HTMLAttributes, type ReactNode } from 'react'

import { getSiteDisplayName, getState } from '../../lib/state'
import {
  usePageFocusState,
  type MountedScreen,
  type PageFocusState,
  type TitleOwner
} from './PageFocusProvider'

export interface PageHeadingProps extends HTMLAttributes<HTMLHeadingElement> {
  children: ReactNode
  /** Distinguishes screens that share a pathname. Defaults to the heading's id. */
  screenId?: string
}

function topOwner(state: PageFocusState): TitleOwner | undefined {
  return state.titleOwners[state.titleOwners.length - 1]
}

function applyTopTitle(state: PageFocusState) {
  const owner = topOwner(state)
  if (owner && document.title !== owner.title) {
    document.title = owner.title
  }
}

/**
 * The page's level-1 heading. On a client-side navigation it takes focus and
 * retitles the document, so a screen reader user hears where they landed.
 * The first heading after a full document load is left alone: the browser
 * already reads a fresh page from the top.
 */
export function PageHeading({ children, screenId, id, ...rest }: PageHeadingProps) {
  const heading = useRef<HTMLHeadingElement>(null)
  const focusState = usePageFocusState()
  const pathname = usePathname()
  const screenKey = `${pathname}#${screenId ?? id ?? ''}`

  useEffect(() => {
    if (!focusState) {
      return
    }
    const state = focusState.current
    const mounted: MountedScreen = { screenKey, element: heading.current }
    state.mountedScreens.push(mounted)
    const previousScreen = state.lastScreen
    state.lastScreen = screenKey
    if (previousScreen !== null && previousScreen !== screenKey) {
      heading.current?.focus({ preventScroll: true })
    }

    return () => {
      state.mountedScreens = state.mountedScreens.filter((entry) => entry !== mounted)
      const outer = state.mountedScreens[state.mountedScreens.length - 1]
      // A nested screen (a loading interstitial) is leaving: the visitor is back on
      // the screen beneath it, so that heading becomes current and takes focus.
      if (state.lastScreen === screenKey && outer) {
        state.lastScreen = outer.screenKey
        outer.element?.focus({ preventScroll: true })
      }
    }
  }, [focusState, screenKey])

  useEffect(() => {
    if (!focusState) {
      return
    }
    const text = heading.current?.textContent?.trim()
    if (!text) {
      return
    }
    const state = focusState.current
    const owner: TitleOwner = { title: `${text} | ${getSiteDisplayName(getState())}` }
    if (state.titleOwners.length === 0) {
      state.frameworkTitle = document.title
    }
    state.titleOwners.push(owner)
    applyTopTitle(state)

    // Next swaps the hoisted <title> once streamed route metadata lands, which can
    // be after this effect. Keep this screen's title and remember the framework's.
    const observer = new MutationObserver(() => {
      if (topOwner(state) !== owner || !document.querySelector('title')) {
        return
      }
      if (document.title !== owner.title) {
        state.frameworkTitle = document.title
        applyTopTitle(state)
      }
    })
    observer.observe(document.head, { childList: true, subtree: true, characterData: true })

    return () => {
      observer.disconnect()
      state.titleOwners = state.titleOwners.filter((entry) => entry !== owner)
      if (state.titleOwners.length === 0) {
        if (state.frameworkTitle !== null) {
          document.title = state.frameworkTitle
        }
      } else {
        applyTopTitle(state)
      }
    }
  }, [focusState, children])

  return (
    <h1
      ref={heading}
      id={id}
      tabIndex={-1}
      {...rest}
    >
      {children}
    </h1>
  )
}
