import { useQuery } from '@tanstack/react-query'
import { getSchools } from '../api/getSchools'

interface UseSchoolsOptions {
  enabled: boolean
  apiBaseUrl: string
}

export function useSchools({ enabled, apiBaseUrl }: UseSchoolsOptions) {
  return useQuery({
    queryKey: ['schools', apiBaseUrl],
    queryFn: () => getSchools(apiBaseUrl),
    enabled,
    staleTime: Infinity  // school list doesn't change within a session
  })
}
