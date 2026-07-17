/** Normalize a name for CBMS (`^[A-Za-z\-'\s]{1,N}$`): strip diacritics, smart quotes → straight, truncate. */
export function sanitizeNameForCbms(name: string, maxLength: number): string {
  return name
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[‘’]/g, "'")
    .slice(0, maxLength)
}

export const CBMS_FIRST_NAME_MAX = 35
export const CBMS_LAST_NAME_MAX = 40
