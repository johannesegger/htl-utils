<script setup lang="ts">
import { onMounted, onUnmounted, ref, toRef } from 'vue'
import { api, type CustomOperation } from '@/api'
import { runExecution, type ExecutionState } from '@/execution'
import ErrorMessage from './ErrorMessage.vue'
import { pluralize } from '@/utils.ts'

type LoadState =
  { type: 'notLoaded' } |
  { type: 'loading' } |
  { type: 'loadError', message: string } |
  { type: 'loaded', operations: ExecutableOperation[] }

interface Calculation {
  data: unknown
  execution: ExecutionState
}

type CalculationState =
  { type: 'notCalculated' } |
  { type: 'calculating', abortController: AbortController } |
  { type: 'calculated', calculations: Calculation[] } |
  { type: 'calculationError', message: string }

type ExecutableOperation = {
  data: CustomOperation
  calculationState: CalculationState
}

const loadState = ref<LoadState>({ type: 'notLoaded' })

const calculatingAll = ref(false)

function isAbort(e: unknown): boolean {
  return e instanceof DOMException && e.name === 'AbortError'
}

async function load() {
  loadState.value = { type: 'loading' }
  try {
    const data = await api.getOperations()
    const operations = data
      .filter((v) => v.canCalculate)
      .map((v) : ExecutableOperation => ({
        data: v,
        calculationState: { type: 'notCalculated' },
      }))
    loadState.value = { type: 'loaded', operations: operations }
  } catch (e) {
    loadState.value = { type: 'loadError', message: (e as Error).message }
  }
}

async function calculateOne(operation: ExecutableOperation) {
  operation.calculationState = { type: 'calculating', abortController: new AbortController() }
  try {
    const data = (await api.calculateOperation(operation.data.name, operation.calculationState.abortController.signal)) as unknown[]
    const calculations = data.map((entry) : Calculation => ({ data: entry, execution: { type: 'notExecuted' } }))
    operation.calculationState = { type: 'calculated', calculations: calculations }
  } catch (e) {
    if (isAbort(e)) {
      operation.calculationState = { type: 'notCalculated' }
    }
    else {
      operation.calculationState = { type: 'calculationError', message: (e as Error).message }
    }
  }
}

function cancelCalculation(operation: ExecutableOperation) {
  if (operation.calculationState.type !== 'calculating') return

  operation.calculationState.abortController.abort()
}

function cancelAllCalculations() {
  if (loadState.value.type !== 'loaded') return

  for (const operation of loadState.value.operations) {
    cancelCalculation(operation)
  }
}

async function calculateAll() {
  if (loadState.value.type !== 'loaded') return

  calculatingAll.value = true
  try {
    await Promise.all(loadState.value.operations.map(operation => calculateOne(operation)))
  } finally {
    calculatingAll.value = false
  }
}

async function executeOne(calculation: Calculation, name: string) {
  if (calculation.execution.type === 'executed') return

  await runExecution(name, calculation.data, toRef(calculation, 'execution'))
}

async function executeGroup(operation: ExecutableOperation) {
  if (operation.calculationState.type !== 'calculated') return

  await Promise.all(
    operation.calculationState.calculations
      .map(calculation => executeOne(calculation, operation.data.name)))
}

onMounted(load)
onUnmounted(cancelAllCalculations)
</script>

<template>
  <section class="space-y-4">
    <div class="flex items-center justify-between">
      <h2 class="text-lg font-semibold">Calculated operations</h2>
      <div class="flex gap-2">
        <button class="btn-secondary" @click="load">Reload</button>
        <button v-if="calculatingAll" class="btn-danger" @click="cancelAllCalculations">Cancel all</button>
        <button v-else-if="loadState.type === 'loaded'"
          class="btn-primary"
          :disabled="loadState.operations.length === 0"
          @click="calculateAll">Calculate all</button>
      </div>
    </div>

    <p v-if="loadState.type === 'loading'" class="text-sm text-gray-500">Loading…</p>
    <ErrorMessage v-else-if="loadState.type === 'loadError'" :message="loadState.message" />

    <div
      v-if="loadState.type === 'loaded' && loadState.operations.length === 0"
      class="rounded border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500">
      No operations with a calculate script.
    </div>

    <div v-if="loadState.type === 'loaded'" v-for="operation in loadState.operations" :key="operation.data.name" class="space-y-1">
      <div class="flex items-center justify-between">
        <h3 class="font-medium">
          {{ operation.data.settings.title }}
          <span v-if="operation.calculationState.type === 'calculated'"
            class="text-xs text-gray-500">
            ({{ pluralize(operation.calculationState.calculations.length, 'operation', 'operations') }} calculated)
          </span>
        </h3>
        <div class="flex gap-2">
          <button v-if="operation.calculationState.type === 'calculated' && operation.calculationState.calculations.some(v => v.execution.type === 'notExecuted' || v.execution.type === 'executionError')"
            class="btn-secondary"
            @click="executeGroup(operation)">Execute all</button>
          <button v-else-if="operation.calculationState.type === 'calculated' && operation.calculationState.calculations.some(v => v.execution.type === 'executing')"
            class="btn-secondary"
            disabled>Executing…</button>

          <button v-if="operation.calculationState.type === 'calculating'"
            class="btn-danger"
            @click="cancelCalculation(operation)">
            Cancel
          </button>
          <button v-else class="btn-secondary" @click="calculateOne(operation)">Calculate</button>
        </div>
      </div>
      <ErrorMessage v-if="operation.calculationState.type === 'calculationError'" :message="operation.calculationState.message" />

      <div v-if="operation.calculationState.type === 'calculated'"
        class="flex flex-col gap-2">
        <div v-for="(calculation, index) in operation.calculationState.calculations" :key="index" class="flex flex-col gap-1">
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
              <button v-if="calculation.execution.type === 'executing'"
                class="btn-secondary"
                disabled>Executing…</button>
              <button v-else-if="calculation.execution.type !== 'executed'"
                class="btn-secondary"
                @click="executeOne(calculation, operation.data.name)">Execute</button>
            </div>
            <pre v-if="calculation.execution.type === 'executed'"
              class="rounded bg-gray-900 p-3 text-xs text-gray-100 whitespace-pre overflow-x-auto"
              >{{ calculation.execution.output ? JSON.stringify(calculation.execution.output, null, 2) : 'Execution succeeded' }}</pre>
            <pre v-else-if="calculation.execution.type === 'executionError'"
              class="rounded bg-gray-900 p-3 text-xs text-red-300 whitespace-pre overflow-x-auto">{{ calculation.execution.message }}</pre>
          </div>
        </div>
      </div>
      <p
        v-else-if="operation.calculationState.type === 'notCalculated'"
        class="text-sm text-gray-500">
        Not calculated yet.
      </p>
    </div>
  </section>
</template>
