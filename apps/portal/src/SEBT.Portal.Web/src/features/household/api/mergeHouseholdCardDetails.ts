import type { HouseholdData, SummerEbtCase } from './schema'

function caseKey(summerEbtCase: SummerEbtCase): string {
  return (
    summerEbtCase.summerEBTCaseID ??
    `${summerEbtCase.childFirstName}|${summerEbtCase.childLastName}|${summerEbtCase.childDateOfBirth}`
  )
}

/**
 * Merges card fields from a full household response into the shell response,
 * preserving portal-hydrated fields (e.g. cardRequestedAt) on the shell cases.
 */
export function mergeHouseholdCardDetails(
  shell: HouseholdData,
  full: HouseholdData
): HouseholdData {
  const fullByKey = new Map(full.summerEbtCases.map((c) => [caseKey(c), c]))

  return {
    ...shell,
    summerEbtCases: shell.summerEbtCases.map((shellCase) => {
      const fullCase = fullByKey.get(caseKey(shellCase))
      if (!fullCase) {
        return shellCase
      }

      return {
        ...shellCase,
        ...(fullCase.ebtCardLastFour != null ? { ebtCardLastFour: fullCase.ebtCardLastFour } : {}),
        ...(fullCase.ebtCardStatus != null ? { ebtCardStatus: fullCase.ebtCardStatus } : {}),
        ...(fullCase.ebtCardIssueDate != null
          ? { ebtCardIssueDate: fullCase.ebtCardIssueDate }
          : {}),
        ...(fullCase.ebtCardBalance != null ? { ebtCardBalance: fullCase.ebtCardBalance } : {})
      }
    })
  }
}
