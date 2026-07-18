<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { api, type OperationOverview } from '@/api'
import ErrorMessage from './ErrorMessage.vue'
import { pluralize } from '@/utils.ts'

interface OpResult {
  calculating: boolean
  data: null | unknown[]
  error: string | null
  hasCalculated: boolean
  controller: AbortController | null
}

const operations = ref<OperationOverview[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const calculatingAll = ref(false)
const results = reactive<Record<string, OpResult>>({})

const calculable = computed(() => operations.value.filter((o) => o.canCalculate))

function resultFor(name: string): OpResult {
  if (!results[name]) results[name] = { calculating: false, data: null, error: null, hasCalculated: false, controller: null }
  return results[name]
}

function isAbort(e: unknown): boolean {
  return e instanceof DOMException && e.name === 'AbortError'
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    operations.value = await api.getOperationOverviews()
  } catch (e) {
    loadError.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function calculateOne(name: string) {
  const result = resultFor(name)
  const controller = new AbortController()
  result.controller = controller
  result.calculating = true
  result.error = null
  try {
    result.data = await api.calculateOperation(name, controller.signal) as unknown[]
    result.hasCalculated = true
  } catch (e) {
    if (!isAbort(e)) {
      result.error = (e as Error).message
      result.hasCalculated = true
    }
  } finally {
    result.calculating = false
    result.controller = null
  }
}

function cancel(name: string) {
  results[name]?.controller?.abort()
}

async function calculateAll() {
  calculatingAll.value = true
  try {
    await Promise.all(calculable.value.map((operation) => calculateOne(operation.name)))
  } finally {
    calculatingAll.value = false
  }
}

function cancelAll() {
  for (const result of Object.values(results)) result.controller?.abort()
}

onMounted(load)
onUnmounted(cancelAll)
</script>

<template>
  <section class="space-y-4">
    <div class="flex items-center justify-between">
      <h2 class="text-lg font-semibold">Calculations</h2>
      <div class="flex gap-2">
        <button class="btn-secondary" @click="load">↻</button>
        <button v-if="calculatingAll" class="btn-danger" @click="cancelAll">Cancel all</button>
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

    <div v-for="operation in calculable" :key="operation.name" class="space-y-1">
      <div class="flex items-center justify-between">
        <h3 class="font-medium">
          {{ operation.name }}
          <span v-if="resultFor(operation.name).hasCalculated && !resultFor(operation.name).error"
            class="text-xs text-gray-500">
            ({{ pluralize(resultFor(operation.name).data?.length ?? 0, 'operation', 'operations') }} calculated)
          </span>
        </h3>
        <button
          v-if="resultFor(operation.name).calculating"
          class="btn-danger"
          @click="cancel(operation.name)">
          Cancel
        </button>
        <button v-else class="btn-secondary" @click="calculateOne(operation.name)">Calculate</button>
      </div>
      <ErrorMessage :message="resultFor(operation.name).error" />
      <div v-if="resultFor(operation.name).hasCalculated && !resultFor(operation.name).error"
        class="flex flex-col gap-2">
        <div v-for="entry in resultFor(operation.name).data"
          :key="JSON.stringify(entry)"
          class="flex gap-4 rounded bg-gray-100 px-3 py-2 text-sm">
          <div v-for="(value, key) in entry" class="flex flex-col">
            <span class="text-xs text-gray-500">{{ key }}</span>
            <span>{{ value }}</span>
          </div>
        </div>
      </div>
      <p
        v-else-if="!resultFor(operation.name).hasCalculated && !resultFor(operation.name).calculating"
        class="text-sm text-gray-500">
        Not calculated yet.
      </p>
    </div>
  </section>
</template>
