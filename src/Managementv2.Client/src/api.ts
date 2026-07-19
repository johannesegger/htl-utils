// Typed client for the custom-operations backend.
//
// Config values are serialized by kind (see the server's CustomOperationsConfig):
//   text                  -> a JSON string
//   file                  -> { file: "<base64>" }
//   credential            -> { userName, password }
//   protected certificate -> { file: "<base64>", password }

import { getFetchHeaderWithAccessToken } from "./auth";

export type WireConfigValue =
  | string
  | { file: string }
  | { userName: string; password: string }
  | { file: string; password: string }

export type WireConfig = Record<string, WireConfigValue>

export interface FormFieldDefinition {
  name: string
  title: string
  type: string
  inputValidations: ('notEmpty')[]
  inputHint?: string
}

export interface FormDefinition {
  title: string
  fields: FormFieldDefinition[]
}

export interface CustomOperation {
  name: string
  form: FormDefinition
  canCalculate: boolean
}

export interface CustomOperationDefinition {
  name: string
  form: FormDefinition
  calculate: string | null
  execute: string
}

export interface CustomOperationDefinitionTemplates {
  formDefinition: FormDefinition
  calculateScript: string
  executeScript: string
}

export interface CustomOperationDefinitions {
  operationDefinitions: CustomOperationDefinition[]
  templates: CustomOperationDefinitionTemplates
}

export interface EditableCustomOperationDefinition {
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
export namespace EditableCustomOperationDefinition {
  export function create(v: CustomOperationDefinition, isNew: boolean) : EditableCustomOperationDefinition {
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

  export function sync(v: EditableCustomOperationDefinition, data: CustomOperationDefinition) {
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

async function fetchAuthenticated(fetchUrl: string, fetchParams?: RequestInit) {
  const authHeader = await getFetchHeaderWithAccessToken()
  const { headers, ...rest } = fetchParams ?? { headers: {} }
  return await fetch(fetchUrl, { headers: { ...authHeader, ...headers }, ...rest})
}

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
  getConfig: () => fetchAuthenticated(`${base}/config`).then((r) => handle<WireConfig>(r)),
  setConfig: (config: WireConfig) =>
    fetchAuthenticated(`${base}/config`, { method: 'PUT', headers: jsonHeaders, body: JSON.stringify(config) }).then((r) =>
      handle<void>(r),
    ),

  getOperations: () => fetchAuthenticated(base).then((r) => handle<CustomOperation[]>(r)),
  getOperationDefinitions: () => fetchAuthenticated(`${base}/definitions`).then((r) => handle<CustomOperationDefinitions>(r)),
  addOperation: (operation: CustomOperationDefinition) =>
    fetchAuthenticated(base, { method: 'POST', headers: jsonHeaders, body: JSON.stringify(operation) }).then((r) =>
      handle<CustomOperationDefinition>(r),
    ),
  updateOperation: (name: string, operation: Omit<CustomOperationDefinition, 'name'>) =>
    fetchAuthenticated(`${base}/${encodeURIComponent(name)}`, {
      method: 'PUT',
      headers: jsonHeaders,
      body: JSON.stringify(operation),
    }).then((r) => handle<CustomOperationDefinition>(r)),
  removeOperation: (name: string) =>
    fetchAuthenticated(`${base}/${encodeURIComponent(name)}`, { method: 'DELETE' }).then((r) => handle<void>(r)),

  calculateOperation: (name: string, signal?: AbortSignal) =>
    fetchAuthenticated(`${base}/${encodeURIComponent(name)}/calculated`, { signal }).then((r) => handle<unknown>(r)),
  execute: (name: string, data: unknown, signal?: AbortSignal) =>
    fetchAuthenticated(`${base}/execution`, {
      method: 'POST',
      headers: jsonHeaders,
      body: JSON.stringify({ name, data }),
      signal,
    }).then((r) => handle<unknown>(r)),
}
