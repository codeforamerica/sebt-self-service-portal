import { expect, type Page } from '@playwright/test'

/** Clicks a USWDS tile radio, which clips the native input, through its associated label. */
async function clickTileRadio(page: Page, name: RegExp) {
  const radio = page.getByRole('radio', { name })
  await radio.scrollIntoViewIfNeeded()
  await page.locator(`label[for="${await radio.getAttribute('id')}"]`).click()
}

/** Fills the DC id-proofing form and submits; waits for the API response. */
export async function submitIdProofingForm(
  page: Page,
  options: {
    month: string
    day: string
    year: string
    idType: 'ssn' | 'itin' | 'snapAccountId' | 'none'
    idValue?: string
  }
) {
  await page.getByRole('combobox', { name: /month/i }).selectOption(options.month)
  await page.locator('[name="dobDay"]').fill(options.day)
  await page.locator('[name="dobYear"]').fill(options.year)

  // "Do you receive SNAP or TANF?" comes first: "Yes" asks for the case number, "No" opens the
  // SSN / ITIN / none options.
  if (options.idType === 'snapAccountId') {
    await clickTileRadio(page, /^Yes$/i)
  } else {
    await clickTileRadio(page, /^No$/i)
    const idTypeLabels: Record<Exclude<typeof options.idType, 'snapAccountId'>, RegExp> = {
      ssn: /Social Security Number \(SSN\)/i,
      itin: /Individual Taxpayer ID Number \(ITIN\)/i,
      none: /^None of the above$/i
    }
    await clickTileRadio(page, idTypeLabels[options.idType])
  }

  if (options.idValue) {
    await page.locator('[name="idValue"]').fill(options.idValue)
  }

  const idProofingResponse = page.waitForResponse(
    (response) =>
      response.url().includes('/api/id-proofing') && response.request().method() === 'POST'
  )

  await page.getByRole('button', { name: /^continue$/i }).click()
  const response = await idProofingResponse
  expect(response.ok(), `ID proofing submit failed with status ${response.status()}`).toBeTruthy()
}
