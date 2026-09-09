<script setup lang="ts">
import { onMounted, ref, useTemplateRef } from 'vue'
import { api, type CustomOperation } from '@/api'
import CalculatedOperation from './CalculatedOperation.vue'
import ErrorMessage from './ErrorMessage.vue'

type LoadState =
  { type: 'notLoaded' } |
  { type: 'loading' } |
  { type: 'loadError', message: string } |
  { type: 'loaded', operations: CustomOperation[] }

const loadState = ref<LoadState>({ type: 'notLoaded' })

const calculatingAll = ref(false)

const operationComponents = useTemplateRef<InstanceType<typeof CalculatedOperation>[]>('operations')

async function load() {
  loadState.value = { type: 'loading' }
  try {
    const data = await api.getOperations()
    loadState.value = { type: 'loaded', operations: data.filter(v => v.canCalculate) }
  } catch (e) {
    loadState.value = { type: 'loadError', message: (e as Error).message }
  }
}

async function calculateAll() {
  if (operationComponents.value === null) return

  calculatingAll.value = true
  try {
    await Promise.all(operationComponents.value.map(operation => operation.calculate()))
  } finally {
    calculatingAll.value = false
  }
}

function cancelAllCalculations() {
  if (operationComponents.value === null) return

  operationComponents.value
    .forEach(operation => operation.cancelCalculation())
}

onMounted(load)
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

    <template v-if="loadState.type === 'loaded'">
      <CalculatedOperation v-for="operation in loadState.operations" :key="operation.name"
        ref="operations"
        :operation="operation" />
    </template>
  </section>
</template>
