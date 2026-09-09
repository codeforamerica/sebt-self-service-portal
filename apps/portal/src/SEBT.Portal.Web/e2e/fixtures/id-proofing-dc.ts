import { expect, type Page } from '@playwright/test'

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

  const idTypeLabels: Record<typeof options.idType, RegExp> = {
    ssn: /Social Security Number \(SSN\)/i,
    itin: /Individual Taxpayer ID Number \(ITIN\)/i,
    snapAccountId: /SNAP or TANF account ID/i,
    none: /^None of the above$/i
  }
  const idTypeRadio = page.getByRole('radio', { name: idTypeLabels[options.idType] })
  await idTypeRadio.scrollIntoViewIfNeeded()
  // USWDS tile radios clip the native input; click the associated label instead.
  await page.locator(`label[for="${await idTypeRadio.getAttribute('id')}"]`).click()

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
