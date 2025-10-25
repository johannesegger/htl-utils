import { getFetchHeaderWithAccessToken } from '@/auth'

export type FetchResult =
  { succeeded: true, response: Response } |
  { succeeded: false, response?: Response }

export const uiFetch = async (fetchUrl: string, fetchParams?: RequestInit) : Promise<FetchResult> => {
  const authHeader = await getFetchHeaderWithAccessToken([])
  try {
    const { headers, ...rest } = fetchParams ?? { headers: {} }
    const response = await fetch(fetchUrl, { headers: { ...authHeader, ...headers }, ...rest})
    return { succeeded: response.ok, response}
  }
  catch {
    return { succeeded: false }
  }
}