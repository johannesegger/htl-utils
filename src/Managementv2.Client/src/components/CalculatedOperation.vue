<script setup lang="ts">
import { computed, onUnmounted, ref, toRef } from 'vue'
import pLimit from 'p-limit'
import { api, maxParallelism, type CustomOperation } from '@/api'
import { runExecution, type ExecutionState } from '@/execution'
import ErrorMessage from './ErrorMessage.vue'
import { pluralize } from '@/utils.ts'

const { operation } = defineProps<{ operation: CustomOperation }>()

type Calculation = {
  data: unknown
  execution: ExecutionState
}

type CalculationState =
  { type: 'notCalculated' } |
  { type: 'calculating', abortController: AbortController } |
  { type: 'calculated', calculations: Calculation[] } |
  { type: 'calculationError', message: string }

const calculationState = ref<CalculationState>({ type: 'notCalculated' })

function countExecutions(type: ExecutionState['type']) {
  if (calculationState.value.type !== 'calculated') return 0

  return calculationState.value.calculations
    .filter(calculation => calculation.execution.type === type)
    .length
}

const succeededCount = computed(() => countExecutions('executed'))
const failedCount = computed(() => countExecutions('executionError'))

const calculateButtonState = computed(() : 'enabled' | 'disabled' | 'cancellable' => {
  if (calculationState.value.type === 'calculating') return 'cancellable'
  if (countExecutions('executing') > 0) return 'disabled'
  return 'enabled'
})

function isAbort(e: unknown): boolean {
  return e instanceof DOMException && e.name === 'AbortError'
}

async function calculate() {
  const abortController = new AbortController()
  calculationState.value = { type: 'calculating', abortController: abortController }
  try {
    const data = (await api.calculateOperation(operation.name, abortController.signal)) as unknown[]
    const calculations = data.map((entry) : Calculation => ({ data: entry, execution: { type: 'notExecuted' } }))
    calculationState.value = { type: 'calculated', calculations: calculations }
  } catch (e) {
    if (isAbort(e)) {
      calculationState.value = { type: 'notCalculated' }
    }
    else {
      calculationState.value = { type: 'calculationError', message: (e as Error).message }
    }
  }
}

function cancelCalculation() {
  if (calculationState.value.type !== 'calculating') return

  calculationState.value.abortController.abort()
}

const limit = pLimit(maxParallelism(operation.settings))

async function executeOne(calculation: Calculation) {
  if (calculation.execution.type === 'queuedForExecution' ||
    calculation.execution.type === 'executing' ||
    calculation.execution.type === 'executed') return

  calculation.execution = { type: 'queuedForExecution' }
  await limit(() => runExecution(operation.name, calculation.data, toRef(calculation, 'execution')))
}

async function executeGroup() {
  if (calculationState.value.type !== 'calculated') return

  await Promise.all(
    calculationState.value.calculations
      .map(calculation => executeOne(calculation))
  )
}

onUnmounted(cancelCalculation)

defineExpose({ calculate, cancelCalculation })
</script>

<template>
  <div class="space-y-1">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-1">
        <h3 class="font-medium">
          {{ operation.settings.title }}
        </h3>
        <span v-if="calculationState.type === 'calculated'"
          class="text-xs text-gray-500">{{ pluralize(calculationState.calculations.length, 'operation', 'operations') }} calculated</span>
        <span v-if="succeededCount > 0" class="text-xs text-green-700">{{ succeededCount }} succeeded</span>
        <span v-if="failedCount > 0" class="text-xs text-red-700">{{ failedCount }} failed</span>
      </div>
      <div class="flex gap-2">
        <button v-if="calculationState.type === 'calculated' && calculationState.calculations.some(v => v.execution.type === 'notExecuted' || v.execution.type === 'executionError')"
          class="btn-secondary"
          @click="executeGroup">Execute all</button>
        <button v-else-if="calculationState.type === 'calculated' && calculationState.calculations.some(v => v.execution.type === 'executing')"
          class="btn-secondary"
          disabled>Executing…</button>

        <button v-if="calculateButtonState === 'enabled'" class="btn-secondary" @click="calculate">Calculate</button>
        <button v-else-if="calculateButtonState === 'disabled'" class="btn-secondary" disabled>Calculate</button>
        <button v-else-if="calculateButtonState === 'cancellable'" class="btn-danger" @click="cancelCalculation">Cancel</button>
      </div>
    </div>
    <ErrorMessage v-if="calculationState.type === 'calculationError'" :message="calculationState.message" />

    <div v-if="calculationState.type === 'calculated'"
      class="flex flex-col gap-2">
      <div v-for="(calculation, index) in calculationState.calculations" :key="index" class="flex flex-col gap-1">
        <div class="flex flex-col gap-2 rounded px-3 py-2 text-sm"
          :class="{
            'bg-gray-100': calculation.execution.type === 'notExecuted' || calculation.execution.type === 'executing',
            'bg-green-100': calculation.execution.type === 'executed',
            'bg-red-100': calculation.execution.type === 'executionError',
            }">
          <div class="flex items-center gap-4">
            <div class="flex flex-1 flex-wrap gap-4">
              <div v-for="(value, key) in calculation.data" :key="key" class="flex flex-col">
                <span class="text-xs text-gray-500">{{ key }}</span>
                <span>{{ value }}</span>
              </div>
            </div>
            <button v-if="calculation.execution.type === 'queuedForExecution'"
              class="btn-secondary"
              disabled>Waiting for execution…</button>
            <button v-else-if="calculation.execution.type === 'executing'"
              class="btn-secondary"
              disabled>Executing…</button>
            <button v-else-if="calculation.execution.type !== 'executed'"
              class="btn-secondary"
              @click="executeOne(calculation)">Execute</button>
          </div>
          <pre v-if="calculation.execution.type === 'executed'"
            class="rounded bg-gray-900 p-3 text-xs text-gray-100 whitespace-pre overflow-x-auto"
            >{{ calculation.execution.output ? JSON.stringify(calculation.execution.output, null, 2) : 'Execution succeeded' }}</pre>
          <pre v-else-if="calculation.execution.type === 'executionError'"
            class="rounded bg-gray-900 p-3 text-xs text-red-300 whitespace-pre overflow-x-auto">{{ calculation.execution.message }}</pre>
        </div>
      </div>
    </div>
    <p v-else-if="calculationState.type === 'notCalculated'" class="text-sm text-gray-500">
      Not calculated yet.
    </p>
  </div>
</template>
