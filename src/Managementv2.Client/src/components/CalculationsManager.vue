<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, toRef } from 'vue'
import { api, type CustomOperation } from '@/api'
import { runExecution, type ExecutionState } from '@/execution'
import ErrorMessage from './ErrorMessage.vue'
import OperationForm from './OperationForm.vue'
import { pluralize } from '@/utils.ts'

interface Calculation {
  data: unknown
  execution: ExecutionState
}

type OperationState =
  { type: 'notCalculated' } |
  { type: 'calculating', abortController: AbortController } |
  { type: 'calculated', calculations: Calculation[] } |
  { type: 'calculationError', message: string }

type ExecutableOperation = {
  data: CustomOperation
  state: OperationState
  showForm: boolean
}

const operations = ref<ExecutableOperation[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const calculatingAll = ref(false)

const calculable = computed(() => operations.value.filter((o) => o.data.calculate !== null))

function isAbort(e: unknown): boolean {
  return e instanceof DOMException && e.name === 'AbortError'
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const data = await api.getOperations()
    operations.value = data.map((v): ExecutableOperation => ({ data: v, state: { type: 'notCalculated' }, showForm: false }))
  } catch (e) {
    loadError.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function calculateOne(operation: ExecutableOperation) {
  operation.state = { type: 'calculating', abortController: new AbortController() }
  try {
    const data = (await api.calculateOperation(operation.data.name, operation.state.abortController.signal)) as unknown[]
    operation.state = {
      type: 'calculated',
      calculations: data.map((entry) => ({ data: entry, execution: { type: 'notExecuted' } }))
    }
  } catch (e) {
    if (isAbort(e)) {
      operation.state = { type: 'notCalculated' }
    }
    else {
      operation.state = { type: 'calculationError', message: (e as Error).message }
    }
  }
}

function cancelCalculation(operation: ExecutableOperation) {
  if (operation.state.type !== 'calculating') return
  operation.state.abortController.abort()
}

function cancelAllCalculations() {
  for (const operation of operations.value) {
    cancelCalculation(operation)
  }
}

async function calculateAll() {
  calculatingAll.value = true
  try {
    await Promise.all(calculable.value.map((operation) => calculateOne(operation)))
  } finally {
    calculatingAll.value = false
  }
}

async function executeOne(calculation: Calculation, name: string) {
  if (calculation.execution.type === 'executed') return
  await runExecution(name, calculation.data, toRef(calculation, 'execution'))
}

async function executeGroup(operation: ExecutableOperation) {
  if (operation.state.type !== 'calculated') return
  await Promise.all(operation.state.calculations.map(calculation => executeOne(calculation, operation.data.name)))
}

onMounted(load)
onUnmounted(cancelAllCalculations)
</script>

<template>
  <section class="space-y-4">
    <div class="flex items-center justify-between">
      <h2 class="text-lg font-semibold">Calculations</h2>
      <div class="flex gap-2">
        <button class="btn-secondary" @click="load">↻</button>
        <button v-if="calculatingAll" class="btn-danger" @click="cancelAllCalculations">Cancel all</button>
        <button v-else class="btn-primary" :disabled="calculable.length === 0" @click="calculateAll">
          Calculate all
        </button>
      </div>
    </div>

    <p v-if="loading" class="text-sm text-gray-500">Loading…</p>
    <ErrorMessage :message="loadError" />

    <div
      v-if="!loading && calculable.length === 0"
      class="rounded border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500">
      No operations with a calculate script.
    </div>

    <div v-for="operation in calculable" :key="operation.data.name" class="space-y-1">
      <div class="flex items-center justify-between">
        <h3 class="font-medium">
          {{ operation.data.form.title }}
          <span v-if="operation.state.type === 'calculated'"
            class="text-xs text-gray-500">
            ({{ pluralize(operation.state.calculations.length, 'operation', 'operations') }} calculated)
          </span>
        </h3>
        <div class="flex gap-2">
          <button v-if="operation.state.type === 'calculated' && operation.state.calculations.some(v => v.execution.type === 'notExecuted' || v.execution.type === 'executionError')"
            class="btn-secondary"
            @click="executeGroup(operation)">Execute all</button>
          <button v-else-if="operation.state.type === 'calculated' && operation.state.calculations.some(v => v.execution.type === 'executing')"
            class="btn-secondary"
            disabled>Executing…</button>

          <button v-if="operation.state.type === 'calculating'"
            class="btn-danger"
            @click="cancelCalculation(operation)">
            Cancel
          </button>
          <button v-else class="btn-secondary" @click="calculateOne(operation)">Calculate</button>
          <OperationForm v-if="operation.data.form.fields.length === 0" :name="operation.data.name" :form="operation.data.form" />
          <button v-else class="btn-secondary" @click="operation.showForm = !operation.showForm">{{ operation.showForm ? 'Hide form' : 'Show form' }}</button>
        </div>
      </div>
      <OperationForm v-if="operation.data.form.fields.length > 0"
        v-show="operation.showForm"
        :name="operation.data.name"
        :form="operation.data.form"
        class="border rounded border-gray-300 p-4" />
      <ErrorMessage v-if="operation.state.type === 'calculationError'" :message="operation.state.message" />
      <div v-if="operation.state.type === 'calculated'"
        class="flex flex-col gap-2">
        <div v-for="(calculation, index) in operation.state.calculations" :key="index" class="flex flex-col gap-1">
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
                class="btn-secondary bg-white"
                disabled>Executing…</button>
              <button v-else-if="calculation.execution.type !== 'executed'"
                class="btn-secondary bg-white"
                @click="executeOne(calculation, operation.data.name)">Execute</button>
            </div>
            <pre v-if="calculation.execution.type === 'executed'"
              class="rounded bg-gray-900 p-3 text-xs text-gray-100 whitespace-pre overflow-x-auto"
              >{{ JSON.stringify(calculation.execution.output, null, 2) }}</pre>
            <p v-else-if="calculation.execution.type === 'executionError'"
              class="rounded bg-gray-900 p-3 text-xs text-red-300 whitespace-pre overflow-x-auto">{{ calculation.execution.message }}</p>
          </div>
        </div>
      </div>
      <p
        v-else-if="operation.state.type === 'notCalculated'"
        class="text-sm text-gray-500">
        Not calculated yet.
      </p>
    </div>
  </section>
</template>
