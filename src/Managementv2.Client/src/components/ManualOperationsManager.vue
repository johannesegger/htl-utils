<script setup lang="ts">
import { onMounted, ref, toRef } from 'vue'
import { api, type FormDefinition, type FormFieldDefinition } from '@/api'
import { runExecution, type ExecutionState } from '@/execution'
import LabeledInput from './LabeledInput.vue'
import ErrorMessage from './ErrorMessage.vue'

type FormField = FormFieldDefinition & { value: string }

type Form = {
  title: string
  fields: FormField[]
}

type ExecutableOperation = {
  name: string
  form: Form
  executionState: ExecutionState
}

type LoadState =
  { type: 'notLoaded' } |
  { type: 'loading' } |
  { type: 'loadError', message: string } |
  { type: 'loaded', operations: ExecutableOperation[] }

const loadState = ref<LoadState>({ type: 'notLoaded' })

function toForm(def: FormDefinition): Form {
  return {
    title: def.title,
    fields: def.fields.map(field => ({ ...field, value: '' })),
  }
}

async function load() {
  loadState.value = { type: 'loading' }
  try {
    const data = await api.getOperations()
    const operations = data.map((v): ExecutableOperation => ({
      name: v.name,
      form: toForm(v.form),
      executionState: { type: 'notExecuted' },
    }))
    loadState.value = { type: 'loaded', operations: operations }
  } catch (e) {
    loadState.value = { type: 'loadError', message: (e as Error).message }
  }
}

function fieldIsValid(field: FormField) {
  return field.inputValidations.every(v => {
    switch (v) {
      case 'notEmpty': return field.value.trim() !== ''
    }
  })
}

function formIsValid(operation: ExecutableOperation) {
  return operation.form.fields.every(field => fieldIsValid(field))
}

async function execute(operation: ExecutableOperation) {
  if (!formIsValid(operation)) return
  const data = Object.fromEntries(operation.form.fields.map(v => [v.name, v.value]))
  await runExecution(operation.name, data, toRef(operation, 'executionState'))
  if (operation.executionState.type === 'executed') {
    operation.form.fields.forEach(v => v.value = '')
  }
}

onMounted(load)
</script>

<template>
  <section class="space-y-4">
    <div class="flex items-center justify-between">
      <h2 class="text-lg font-semibold">Manual operations</h2>
      <button class="btn-secondary" @click="load">Reload</button>
    </div>

    <p v-if="loadState.type === 'loading'" class="text-sm text-gray-500">Loading…</p>
    <ErrorMessage v-else-if="loadState.type === 'loadError'" :message="loadState.message" />

    <div
      v-if="loadState.type === 'loaded' && loadState.operations.length === 0"
      class="rounded border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500">
      No operations.
    </div>

    <form v-if="loadState.type === 'loaded'" v-for="operation in loadState.operations" :key="operation.name"
      class="space-y-3 rounded border border-gray-300 p-4"
      @submit.prevent="execute(operation)">
      <h3 class="font-medium">{{ operation.form.title || operation.name }}</h3>

      <LabeledInput v-for="field in operation.form.fields" :key="field.name" :label="field.title ?? field.name">
        <input
          v-model="field.value"
          class="input w-full"
          :type="field.type === 'password' ? 'password' : 'text'"
          :required="field.inputValidations.includes('notEmpty')"
          autocomplete="off"
          autocorrect="off"
          autocapitalize="off"
          spellcheck="false" />
        <p v-if="field.inputHint" class="mt-1 text-xs text-gray-500">{{ field.inputHint }}</p>
      </LabeledInput>

      <button type="submit" class="btn-primary"
        :disabled="!formIsValid(operation) || operation.executionState.type === 'executing'">
        {{ operation.executionState.type === 'executing' ? 'Executing…' : 'Execute' }}
      </button>

      <ErrorMessage v-if="operation.executionState.type === 'executionError'" :message="operation.executionState.message" />
      <pre
        v-if="operation.executionState.type === 'executed'"
        class="rounded bg-gray-900 p-3 text-xs text-gray-100 whitespace-pre overflow-x-auto"
        >{{ operation.executionState.output ? JSON.stringify(operation.executionState.output, null, 2) : 'Execution succeeded' }}</pre>
    </form>
  </section>
</template>
