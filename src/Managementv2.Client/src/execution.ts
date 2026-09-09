import type { Ref } from 'vue'
import { api } from '@/api'

export type ExecutionState =
  | { type: 'notExecuted' }
  | { type: 'queuedForExecution' }
  | { type: 'executing' }
  | { type: 'executed'; output: unknown }
  | { type: 'executionError'; message: string }

export async function runExecution(id: string, data: unknown, state: Ref<ExecutionState>): Promise<void> {
  if (state.value.type === 'executing') return

  state.value = { type: 'executing' }
  try {
    const output = await api.execute(id, data)
    state.value = { type: 'executed', output: output }
  } catch (e) {
    state.value = { type: 'executionError', message: (e as Error).message }
  }
}
