// Typed client for the custom-operations backend.
//
// Config values are serialized by kind (see the server's CustomOperationsConfig):
//   text                  -> a JSON string
//   file                  -> { file: "<base64>" }
//   credential            -> { userName, password }
//   protected certificate -> { file: "<base64>", password }

export type WireConfigValue =
  | string
  | { file: string }
  | { userName: string; password: string }
  | { file: string; password: string }

export type WireConfig = Record<string, WireConfigValue>

export interface CustomOperation {
  name: string
  form: unknown
  calculate: string | null
  execute: string
}

export interface OperationOverview {
  name: string
  canCalculate: boolean
}

export interface EditableCustomOperation {
  isNew: boolean
  name: string
  form: string
  calculate: string
  execute: string

  inputText: string
  executeResult: string | null
  calculateResult: string | null
  runningCalculate: boolean
  runningExecute: boolean
  calculateController: AbortController | null
  executeController: AbortController | null

  saveError: string | null
  calculateError: string | null
  executeError: string | null
  message: string | null
}
export namespace EditableCustomOperation {
  export function create(v: CustomOperation, isNew: boolean) : EditableCustomOperation {
    return {
      isNew: isNew,
      name: v.name,
      form: JSON.stringify(v.form, null, 2),
      calculate: v.calculate ?? '',
      execute: v.execute,

      inputText: '{}',
      executeResult: null,
      calculateResult: null,
      runningCalculate: false,
      runningExecute: false,
      calculateController: null,
      executeController: null,

      saveError: null,
      calculateError: null,
      executeError: null,
      message: null,
    }
  }

  export function sync(v: EditableCustomOperation, data: CustomOperation) {
    v.isNew = false
    v.name = data.name
    v.form = JSON.stringify(data.form, null, 2)
    v.calculate = data.calculate ?? ''
    v.execute = data.execute
  }
}

export interface CalculatedOperations {
  operations: { name: string; data: unknown }[]
  errors: { operation: string; message: string }[]
}

const base = '/api/custom-operations'
const jsonHeaders = { 'Content-Type': 'application/json' }

async function handle<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const message = await response.text()
    throw new Error(message || `${response.status} ${response.statusText}`)
  }
  if (response.status === 204) return undefined as T
  const text = await response.text()
  return text ? (JSON.parse(text) as T) : (undefined as T)
}

export const api = {
  getConfig: () => fetch(`${base}/config`).then((r) => handle<WireConfig>(r)),
  setConfig: (config: WireConfig) =>
    fetch(`${base}/config`, { method: 'PUT', headers: jsonHeaders, body: JSON.stringify(config) }).then((r) =>
      handle<void>(r),
    ),

  getOperations: () => fetch(`${base}/full`).then((r) => handle<CustomOperation[]>(r)),
  getOperationOverviews: () => fetch(base).then((r) => handle<OperationOverview[]>(r)),
  addOperation: (operation: CustomOperation) =>
    fetch(base, { method: 'POST', headers: jsonHeaders, body: JSON.stringify(operation) }).then((r) =>
      handle<CustomOperation>(r),
    ),
  updateOperation: (name: string, operation: Omit<CustomOperation, 'name'>) =>
    fetch(`${base}/${encodeURIComponent(name)}`, {
      method: 'PUT',
      headers: jsonHeaders,
      body: JSON.stringify(operation),
    }).then((r) => handle<CustomOperation>(r)),
  removeOperation: (name: string) =>
    fetch(`${base}/${encodeURIComponent(name)}`, { method: 'DELETE' }).then((r) => handle<void>(r)),

  calculate: (signal?: AbortSignal) =>
    fetch(`${base}/calculated`, { signal }).then((r) => handle<CalculatedOperations>(r)),
  calculateOperation: (name: string, signal?: AbortSignal) =>
    fetch(`${base}/${encodeURIComponent(name)}/calculated`, { signal }).then((r) => handle<unknown>(r)),
  execute: (name: string, data: unknown, signal?: AbortSignal) =>
    fetch(`${base}/execution`, {
      method: 'POST',
      headers: jsonHeaders,
      body: JSON.stringify({ name, data }),
      signal,
    }).then((r) => handle<unknown>(r)),
}
