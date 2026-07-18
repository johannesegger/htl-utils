<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api, EditableCustomOperation, type CustomOperation } from '@/api'
import ErrorMessage from './ErrorMessage.vue'
import OperationsForm from './OperationsForm.vue'

const operations = ref<EditableCustomOperation[]>([])
const newOperation = ref<EditableCustomOperation>(EditableCustomOperation.create({
  name: '',
  form: { 'fields': [] },
  calculate: '',
  execute: ''
}, true))
const selectedOperation = ref<EditableCustomOperation | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const loadedOperations = await api.getOperations()
    operations.value = loadedOperations.map(v => EditableCustomOperation.create(v, false))
  } catch (e) {
    loadError.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

function removeOperation(operation: EditableCustomOperation) {
  const index = operations.value.indexOf(operation)
  if (index >= 0) {
    operations.value.splice(index, 1)
  }
  selectedOperation.value = null
}

function addOperation(operation: EditableCustomOperation) {
  operations.value.push(operation)
  newOperation.value = EditableCustomOperation.create({
    name: '',
    form: { 'fields': [] },
    calculate: '',
    execute: ''
  }, true)
  selectedOperation.value = operation
}

onMounted(load)
</script>

<template>
  <section class="space-y-4">
    <div class="space-y-2">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">Operations</h2>
        <button class="btn-secondary" @click="load">↻</button>
      </div>
      <div class="flex flex-wrap gap-2">
        <p v-if="loading" class="text-sm text-gray-500 self-center">Loading…</p>
        <button
          v-for="operation in operations"
          :key="operation.name"
          class="rounded border px-4 py-3 text-sm cursor-pointer"
          :class="
            operation === selectedOperation
              ? 'border-blue-600 bg-blue-100 font-medium hover:bg-blue-200'
              : 'border-gray-300 hover:bg-gray-100'"
          @click="selectedOperation = operation"
        >
          {{ operation.name }}
          <span v-if="operation.calculate" class="ml-1 text-xs text-gray-400">(calc)</span>
        </button>
        <button
          class="rounded border border-dashed border-gray-400 px-4 py-3 text-sm text-gray-600 cursor-pointer"
          :class="
            newOperation === selectedOperation
              ? 'border-blue-600 bg-blue-100 font-medium hover:bg-blue-200'
              : 'border-gray-300 hover:bg-gray-100'"
          @click="selectedOperation = newOperation"
        >
          + New operation
        </button>
      </div>
    </div>

    <ErrorMessage :message="loadError" />

    <OperationsForm v-if="selectedOperation"
      v-model="selectedOperation"
      @remove="removeOperation"
      @add="addOperation" />
    <div v-else class="rounded border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500">
      Select an operation or create a new one.
    </div>
  </section>
</template>
