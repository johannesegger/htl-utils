<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { api, type OperationOverview } from '@/api'
import ErrorMessage from './ErrorMessage.vue'

interface OpResult {
  running: boolean
  data: unknown
  error: string | null
  hasRun: boolean
  controller: AbortController | null
}

const operations = ref<OperationOverview[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const runningAll = ref(false)
const results = reactive<Record<string, OpResult>>({})

const calculable = computed(() => operations.value.filter((o) => o.canCalculate))

function resultFor(name: string): OpResult {
  if (!results[name]) results[name] = { running: false, data: null, error: null, hasRun: false, controller: null }
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

async function runOne(name: string) {
  const result = resultFor(name)
  const controller = new AbortController()
  result.controller = controller
  result.running = true
  result.error = null
  try {
    result.data = await api.calculateOperation(name, controller.signal)
    result.hasRun = true
  } catch (e) {
    if (!isAbort(e)) {
      result.error = (e as Error).message
      result.hasRun = true
    }
  } finally {
    result.running = false
    result.controller = null
  }
}

function cancel(name: string) {
  results[name]?.controller?.abort()
}

async function runAll() {
  runningAll.value = true
  try {
    await Promise.all(calculable.value.map((operation) => runOne(operation.name)))
  } finally {
    runningAll.value = false
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
        <button v-if="runningAll" class="btn-danger" @click="cancelAll">Cancel all</button>
        <button v-else class="btn-primary" :disabled="calculable.length === 0" @click="runAll">
          Run all calculations
        </button>
      </div>
    </div>

    <p v-if="loading" class="text-sm text-gray-500">Loading…</p>
    <ErrorMessage :message="loadError" />

    <div
      v-if="!loading && calculable.length === 0"
      class="rounded border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500"
    >
      No operations with a calculate script.
    </div>

    <div v-for="operation in calculable" :key="operation.name" class="space-y-1">
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-medium">{{ operation.name }}</h3>
        <button
          v-if="resultFor(operation.name).running"
          class="btn-danger"
          @click="cancel(operation.name)"
        >
          Cancel
        </button>
        <button v-else class="btn-secondary" @click="runOne(operation.name)">Run</button>
      </div>
      <ErrorMessage :message="resultFor(operation.name).error" />
      <pre
        v-if="resultFor(operation.name).hasRun && !resultFor(operation.name).error"
        class="rounded bg-gray-100 px-3 py-2 text-sm overflow-x-auto"
        >{{ JSON.stringify(resultFor(operation.name).data, null, 2) }}</pre
      >
      <p
        v-else-if="!resultFor(operation.name).hasRun && !resultFor(operation.name).running"
        class="text-sm text-gray-400"
      >
        Not run yet.
      </p>
    </div>
  </section>
</template>
