import type { Page } from '@playwright/test'

export interface AddressFormEntry {
  street: string
  city: string
  state: string
  zip: string
}

/**
 * Fills and submits the address form. The form submits via
 * PUT /api/household/address and navigates by the response's validation status.
 */
export async function fillAndSubmitAddressForm(page: Page, entry: AddressFormEntry): Promise<void> {
  await page.fill('[name="streetAddress1"]', entry.street)
  await page.fill('[name="city"]', entry.city)
  await page.selectOption('[name="state"]', entry.state)
  await page.fill('[name="postalCode"]', entry.zip)
  await page.getByRole('button', { name: 'Continue' }).click()
}
