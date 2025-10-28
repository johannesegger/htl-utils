import { getFetchHeaderWithAccessToken } from '@/auth'
import { ref, type Ref } from 'vue'

export const uiFetch = async (fetchUrl: string, fetchParams?: RequestInit) : Promise<Response> => {
  const authHeader = await getFetchHeaderWithAccessToken([])
  const { headers, ...rest } = fetchParams ?? { headers: {} }
  return await fetch(fetchUrl, { headers: { ...authHeader, ...headers }, ...rest})
}

export type Result<TOk, TErr> =
  { type: 'ok', value: TOk } |
  { type: 'error', error: TErr }
export namespace Result {
  export const ok = <TOk, TErr>(v: TOk) : Result<TOk, TErr> => ({ type: 'ok', value: v })
  export const error = <TOk, TErr>(e: TErr) : Result<TOk, TErr> => ({ type: 'error', error: e })
}
export type WorkflowError<T> =
  { type: 'expected', error: T } |
  { type: 'unexpected', error: unknown }
export type WorkflowResult<TResult, TError> =
  { succeeded: true, result: TResult } |
  { succeeded: false, error: WorkflowError<TError> }
export type Workflow<TResult, TError> = {
  isRunning: Ref<boolean>
  run: () => Promise<void>
  result: Ref<WorkflowResult<TResult, TError> | undefined>
}
export namespace Workflow {
  export const init = <TResult, TError>(fn: () => Promise<Result<TResult, TError>>) : Workflow<TResult, TError> => {
    const isRunning = ref(false)
    const result = ref<WorkflowResult<TResult, TError>>()
    return {
      isRunning,
      run: async () => {
        isRunning.value = true
        try {
          const runResult = await fn()
          switch (runResult.type)
          {
            case 'ok':
              result.value = { succeeded: true, result: runResult.value }
              break
            case 'error':
              result.value = { succeeded: false, error: { type: 'expected', error: runResult.error } }
              break
          }
        }
        catch(e) {
          result.value = { succeeded: false, error: { type: 'unexpected', error: e } }
        }
        finally {
          isRunning.value = false
        }
      },
      result,
    }
  }
}
