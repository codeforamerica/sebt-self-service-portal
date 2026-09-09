import { z } from 'zod'

const schoolSchema = z.object({
  name: z.string(),
  code: z.string()
})

const schoolListSchema = z.array(schoolSchema)

export type School = z.infer<typeof schoolSchema>

export async function getSchools(apiBaseUrl: string): Promise<School[]> {
  const url = `${apiBaseUrl}/api/enrollment/schools`
  const response = await fetch(url)
  if (!response.ok) throw new Error(`getSchools failed: ${response.status.toString()}`)
  const data: unknown = await response.json()
  return schoolListSchema.parse(data)
}
