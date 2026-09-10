<script setup lang="ts">
import { computed, defineAsyncComponent, onUnmounted } from 'vue'
import { api, EditableCustomOperationDefinition, type FormFieldDefinition, type OperationSettings } from '@/api.ts'
import LabeledInput from './LabeledInput.vue'
import ErrorMessage from './ErrorMessage.vue';

// Monaco is big, so keep it out of the initial bundle - it's only needed when editing an operation.
const CodeEditor = defineAsyncComponent(() => import('./CodeEditor.vue'))

const operation = defineModel<EditableCustomOperationDefinition>({ required: true })

const emit = defineEmits<{
  (e: 'add', v: EditableCustomOperationDefinition): void
  (e: 'remove', v: EditableCustomOperationDefinition): void
}>()

const running = computed(() => operation.value.runningCalculate || operation.value.runningExecute)

const calculateScript = computed(() => operation.value.calculate.trim() === '' ? null : operation.value.calculate)

function parseJson(text: string, what: string): unknown {
  try {
    return JSON.parse(text)
  } catch {
    throw new Error(`${what} is not valid JSON.`)
  }
}

function readSettings(): OperationSettings {
  const executionForm = parseJson(operation.value.executionForm, 'The execution form')
  return {
    title: operation.value.title,
    executionForm: executionForm as FormFieldDefinition[],
    maxParallelism: operation.value.maxParallelism,
  }
}

async function save() {
  operation.value.saveError = null
  operation.value.message = null
  try {
    const settings = readSettings()
    if (operation.value.execute.trim() === '') throw new Error('An execute script is required.')
    let saved
    if (operation.value.isNew) {
      saved = await api.addOperation({
        settings: settings,
        calculate: calculateScript.value,
        execute: operation.value.execute,
      })
      EditableCustomOperationDefinition.sync(operation.value, saved)
      emit('add', operation.value)
    } else {
      saved = await api.updateOperation(operation.value.id, {
        settings: settings,
        calculate: calculateScript.value,
        execute: operation.value.execute,
      })
    }
    operation.value.message = 'Operation saved.'
  } catch (e) {
    operation.value.saveError = (e as Error).message
  }
}

async function remove() {
  if (!operation.value.id) return
  if (!confirm(`Delete operation "${operation.value.title || operation.value.id}"?`)) return
  operation.value.saveError = null
  try {
    await api.removeOperation(operation.value.id)
    emit('remove', operation.value)
  } catch (e) {
    operation.value.saveError = (e as Error).message
  }
}

function isAbort(e: unknown): boolean {
  return e instanceof DOMException && e.name === 'AbortError'
}

async function runCalculate() {
  if (!operation.value.id) return
  if (!calculateScript.value) return

  await save()
  if (operation.value.saveError) return

  const controller = new AbortController()
  operation.value.calculateController = controller
  operation.value.runningCalculate = true
  operation.value.calculateError = null
  operation.value.calculateResult = null
  try {
    const result = await api.calculateOperation(operation.value.id, controller.signal)
    operation.value.calculateResult = result === undefined ? '(no calculate script)' : JSON.stringify(result, null, 2)
  } catch (e) {
    if (!isAbort(e)) operation.value.calculateError = (e as Error).message
  } finally {
    operation.value.runningCalculate = false
    operation.value.calculateController = null
  }
}

function cancelCalculate() {
  operation.value.calculateController?.abort()
}

async function runExecute() {
  if (!operation.value.id) return

  await save()
  if (operation.value.saveError) return

  const controller = new AbortController()
  operation.value.executeController = controller
  operation.value.runningExecute = true
  operation.value.executeError = null
  operation.value.executeResult = null
  try {
    const data = parseJson(operation.value.inputText, 'The input data')
    const result = await api.execute(operation.value.id, data, controller.signal)
    operation.value.executeResult = result ? JSON.stringify(result, null, 2) : '<No output>'
  } catch (e) {
    if (!isAbort(e)) operation.value.executeError = (e as Error).message
  } finally {
    operation.value.runningExecute = false
    operation.value.executeController = null
  }
}

function cancelExecute() {
  operation.value.executeController?.abort()
}

onUnmounted(() => {
  cancelCalculate()
  cancelExecute()
})
</script>

<template>
  <div class="space-y-3 rounded border border-gray-200 p-4">
    <p v-if="operation.message" class="rounded bg-green-100 px-3 py-2 text-sm text-green-800">{{ operation.message }}</p>

    <LabeledInput v-if="!operation.isNew" label="Id">
      <input :value="operation.id" disabled class="input w-full disabled:bg-gray-100" />
    </LabeledInput>
    <LabeledInput label="Title">
      <input v-model="operation.title" placeholder="Create teacher" class="input w-full" />
    </LabeledInput>
    <LabeledInput label="Execution form (JSON)">
      <CodeEditor v-model="operation.executionForm" language="json" :lines="10" />
    </LabeledInput>
    <LabeledInput label="Number of executions of this operation that may run at the same time">
      <input v-model.number="operation.maxParallelism" type="number" min="1" step="1" class="input w-20 self-start" />
    </LabeledInput>
    <LabeledInput label="Calculate script (optional, PowerShell)">
      <CodeEditor v-model="operation.calculate" language="powershell" :lines="12" />
    </LabeledInput>
    <LabeledInput label="Execute script (PowerShell)">
      <CodeEditor v-model="operation.execute" language="powershell" :lines="16" />
    </LabeledInput>
    <div class="flex gap-2">
      <button class="btn-primary" @click="save">Save</button>
      <button v-if="!operation.isNew" class="btn-danger" @click="remove">Delete</button>
    </div>
    <ErrorMessage :message="operation.saveError" />
  </div>

  <div v-if="!operation.isNew" class="space-y-3 rounded border border-gray-200 p-4">
    <h3 class="text-sm font-semibold">Test scripts</h3>

    <button v-if="operation.runningCalculate" class="btn-danger" @click="cancelCalculate">Cancel</button>
    <button v-else class="btn-secondary" :disabled="running || !calculateScript" @click="runCalculate">Run calculate</button>
    <pre
      v-if="operation.calculateResult"
      class="mt-2 max-h-80 overflow-auto rounded bg-gray-900 p-3 text-xs text-gray-100"
      >{{ operation.calculateResult }}</pre>
    <ErrorMessage :message="operation.calculateError" />

    <hr class="border-gray-500" />

    <LabeledInput label="Input data (JSON)">
      <CodeEditor v-model="operation.inputText" language="json" :lines="5" placeholder='{ "userName": "eina" }' />
    </LabeledInput>
    <button v-if="operation.runningExecute" class="btn-danger" @click="cancelExecute">Cancel</button>
    <button v-else class="btn-secondary" :disabled="running" @click="runExecute">Run execute</button>
    <pre v-if="operation.executeResult" class="max-h-80 overflow-auto rounded bg-gray-900 p-3 text-xs text-gray-100">{{ operation.executeResult }}</pre>
    <ErrorMessage :message="operation.executeError" />
  </div>
</template>