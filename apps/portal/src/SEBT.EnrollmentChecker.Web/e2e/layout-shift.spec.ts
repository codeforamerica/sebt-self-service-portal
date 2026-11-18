import { expect, test, type Page } from '@playwright/test'

// Nothing already painted may move when the first features poll resolves. The
// poll is delayed so the hold is observable, and layout-shift entries are summed
// by an observer installed before the first paint.
const FEATURES_DELAY_MS = 1500

// The header settles by a few pixels early in load regardless of the poll (about
// 2e-5 at 1920px); the regression this guards scored 0.16 to 0.41.
const MAX_LAYOUT_SHIFT = 0.01

const openSeasonFeatures = {
  maintenanceBanner: { enabled: false, message: {} },
  outagePage: { enabled: false },
  apply: { enabled: true },
  enrollment: { enabled: true }
}

type ShiftWindow = Window & { __layoutShift?: number }

async function installLayoutShiftObserver(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const w = window as ShiftWindow
    w.__layoutShift = 0
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        const shift = entry as PerformanceEntry & { hadRecentInput?: boolean; value?: number }
        if (!shift.hadRecentInput) {
          w.__layoutShift = (w.__layoutShift ?? 0) + (shift.value ?? 0)
        }
      }
    }).observe({ type: 'layout-shift', buffered: true })
  })
}

async function delayFeaturesPoll(page: Page): Promise<void> {
  await page.route('**/api/enrollment/features*', async (route) => {
    await new Promise((resolve) => setTimeout(resolve, FEATURES_DELAY_MS))
    await route.fulfill({ json: openSeasonFeatures })
  })
}

for (const viewport of [
  { width: 1419, height: 858 },
  { width: 390, height: 844 }
]) {
  test(`landing page first paint does not shift at ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport)
    await installLayoutShiftObserver(page)
    await delayFeaturesPoll(page)

    await page.goto('/')
    // The footer must not be in the page before the season is known; painting it
    // under an empty main is what the content pushes down when it arrives.
    const footersBeforePoll = await page.getByRole('contentinfo').count()

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await expect(page.getByRole('contentinfo')).toBeVisible()

    const shift = await page.evaluate(() => (window as ShiftWindow).__layoutShift ?? 0)
    expect(shift, `cumulative layout shift ${shift}`).toBeLessThan(MAX_LAYOUT_SHIFT)
    expect(footersBeforePoll, 'footer rendered before the season was known').toBe(0)
  })
}
