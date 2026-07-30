/**
 * In-memory hand-off of just-replaced cards from the confirm screen to the
 * dashboard flash banner. Child names and card digits are PII, so they must
 * not travel through URL params or web storage; module state lives only for
 * the client-side navigation and is gone after a full page load.
 */
export interface ReplacedCardFlash {
  childFirstName: string
  childLastName: string
  ebtCardLastFour: string | null
}

let flash: ReplacedCardFlash[] = []

export function setReplacementFlash(cards: ReplacedCardFlash[]): void {
  flash = [...cards]
}

/**
 * Reading leaves the value in place (React StrictMode double-invokes state
 * initializers); the consumer clears it in an effect once mounted.
 */
export function getReplacementFlash(): ReplacedCardFlash[] {
  return [...flash]
}

export function clearReplacementFlash(): void {
  flash = []
}
