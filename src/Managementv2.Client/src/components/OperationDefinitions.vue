<script setup lang="ts">
import { onMounted, ref, toRaw } from 'vue'
import { api, EditableCustomOperationDefinition, type CustomOperationDefinitionTemplates } from '@/api'
import ErrorMessage from './ErrorMessage.vue'
import EditOperationForm from './EditOperationForm.vue'

type LoadState =
  { type: 'notLoaded' } |
  { type: 'loading' } |
  { type: 'loadError', message: string } |
  {
    type: 'loaded',
    operations: EditableCustomOperationDefinition[],
    templates: CustomOperationDefinitionTemplates,
    newOperation: EditableCustomOperationDefinition,
    selectedOperation: EditableCustomOperationDefinition | null,
  }

const loadState = ref<LoadState>({ type: 'notLoaded' })

async function load() {
  loadState.value = { type: 'loading' }
  try {
    const loadedOperations = await api.getOperationDefinitions()
    const operations = loadedOperations.operationDefinitions.map(v => EditableCustomOperationDefinition.create(v, false))
    const newOperation = EditableCustomOperationDefinition.create({
      name: '',
      settings: structuredClone(loadedOperations.templates.settings),
      calculate: loadedOperations.templates.calculateScript,
      execute: loadedOperations.templates.executeScript,
    }, true)
    loadState.value = { type: 'loaded', operations: operations, templates: loadedOperations.templates, newOperation: newOperation, selectedOperation: null }
  } catch (e) {
    loadState.value = { type: 'loadError', message: (e as Error).message }
  }
}

function removeOperation(operation: EditableCustomOperationDefinition) {
  if (loadState.value.type !== 'loaded') return

  const index = loadState.value.operations.indexOf(operation)
  if (index >= 0) {
    loadState.value.operations.splice(index, 1)
  }
  loadState.value.selectedOperation = null
}

function addOperation(operation: EditableCustomOperationDefinition) {
  if (loadState.value.type !== 'loaded') return

  loadState.value.operations.push(operation)
  loadState.value.newOperation = EditableCustomOperationDefinition.create({
    name: '',
    settings: structuredClone(toRaw(loadState.value.templates.settings)),
    calculate: loadState.value.templates.calculateScript,
    execute: loadState.value.templates.executeScript,
  }, true)
  loadState.value.selectedOperation = operation
}

onMounted(load)
</script>

<template>
  <section class="space-y-4">
    <div class="space-y-2">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">Operations</h2>
        <button class="btn-secondary" @click="load">Reload</button>
      </div>
      <p v-if="loadState.type === 'loading'" class="text-sm text-gray-500 self-center">Loading…</p>
      <div v-else-if="loadState.type === 'loaded'" class="flex flex-wrap gap-2">
        <button v-for="operation in loadState.operations"
          :key="operation.name"
          class="rounded border px-4 py-3 text-sm cursor-pointer"
          :class="
            operation === loadState.selectedOperation
              ? 'border-blue-600 bg-blue-100 font-medium hover:bg-blue-200'
              : 'border-gray-300 hover:bg-gray-100'"
          @click="loadState.selectedOperation = operation">
          {{ operation.name }}
          <span v-if="operation.calculate" class="ml-1 text-xs text-gray-400">(calc)</span>
        </button>
        <button
          class="rounded border border-dashed border-gray-400 px-4 py-3 text-sm text-gray-600 cursor-pointer"
          :class="
            loadState.newOperation === loadState.selectedOperation
              ? 'border-blue-600 bg-blue-100 font-medium hover:bg-blue-200'
              : 'border-gray-300 hover:bg-gray-100'"
          @click="loadState.selectedOperation = loadState.newOperation">+ New operation</button>
      </div>
    </div>

    <ErrorMessage v-if="loadState.type === 'loadError'" :message="loadState.message" />

    <template v-if="loadState.type === 'loaded'">
      <EditOperationForm v-if="loadState.selectedOperation"
        v-model="loadState.selectedOperation"
        @remove="removeOperation"
        @add="addOperation" />
      <div v-else class="rounded border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500">
        Select an operation or create a new one.
      </div>
    </template>
  </section>
</template>
