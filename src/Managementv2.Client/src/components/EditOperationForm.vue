<script setup lang="ts">
import { computed, onUnmounted } from 'vue'
import { api, EditableCustomOperationDefinition, type FormDefinition } from '@/api.ts'
import LabeledInput from './LabeledInput.vue'
import ErrorMessage from './ErrorMessage.vue';

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

async function save() {
  operation.value.saveError = null
  operation.value.message = null
  try {
    const form = parseJson(operation.value.form, 'The form definition') as FormDefinition
    if (operation.value.execute.trim() === '') throw new Error('An execute script is required.')
    let saved
    if (operation.value.isNew) {
      if (operation.value.name.trim() === '') throw new Error('A name is required.')
      saved = await api.addOperation({ name: operation.value.name.trim(), form: form, calculate: calculateScript.value, execute: operation.value.execute })
      EditableCustomOperationDefinition.sync(operation.value, saved)
      emit('add', operation.value)
    } else {
      saved = await api.updateOperation(operation.value.name, { form: form, calculate: calculateScript.value, execute: operation.value.execute })
    }
    operation.value.message = 'Operation saved.'
  } catch (e) {
    operation.value.saveError = (e as Error).message
  }
}

async function remove() {
  if (!operation.value.name) return
  if (!confirm(`Delete operation "${operation.value.name}"?`)) return
  operation.value.saveError = null
  try {
    await api.removeOperation(operation.value.name)
    emit('remove', operation.value)
  } catch (e) {
    operation.value.saveError = (e as Error).message
  }
}

function isAbort(e: unknown): boolean {
  return e instanceof DOMException && e.name === 'AbortError'
}

async function runCalculate() {
  if (!operation.value.name) return
  if (!calculateScript.value) return

  await save()
  if (operation.value.saveError) return

  const controller = new AbortController()
  operation.value.calculateController = controller
  operation.value.runningCalculate = true
  operation.value.calculateError = null
  operation.value.calculateResult = null
  try {
    const result = await api.calculateOperation(operation.value.name, controller.signal)
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
  if (!operation.value.name) return

  await save()
  if (operation.value.saveError) return

  const controller = new AbortController()
  operation.value.executeController = controller
  operation.value.runningExecute = true
  operation.value.executeError = null
  operation.value.executeResult = null
  try {
    const data = parseJson(operation.value.inputText, 'The input data')
    const result = await api.execute(operation.value.name, data, controller.signal)
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

    <LabeledInput label="Name">
      <input
        v-model="operation.name"
        :disabled="!operation.isNew"
        placeholder="operation-name"
        class="input w-full disabled:bg-gray-100"
      />
    </LabeledInput>
    <LabeledInput label="Form definition (JSON)">
      <textarea v-model="operation.form" rows="12" class="textarea" autocomplete="off" autocorrect="off" autocapitalize="off" spellcheck="false"></textarea>
    </LabeledInput>
    <LabeledInput label="Calculate script (optional, PowerShell)">
      <textarea v-model="operation.calculate" rows="12" class="textarea" autocomplete="off" autocorrect="off" autocapitalize="off" spellcheck="false"></textarea>
    </LabeledInput>
    <LabeledInput label="Execute script (PowerShell)">
      <textarea v-model="operation.execute" rows="16" class="textarea" autocomplete="off" autocorrect="off" autocapitalize="off" spellcheck="false"></textarea>
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
      <textarea v-model="operation.inputText" rows="5" class="textarea" placeholder='{ "userName": "eina" }'></textarea>
    </LabeledInput>
    <button v-if="operation.runningExecute" class="btn-danger" @click="cancelExecute">Cancel</button>
    <button v-else class="btn-secondary" :disabled="running" @click="runExecute">Run execute</button>
    <pre v-if="operation.executeResult" class="max-h-80 overflow-auto rounded bg-gray-900 p-3 text-xs text-gray-100">{{ operation.executeResult }}</pre>
    <ErrorMessage :message="operation.executeError" />
  </div>
</template>