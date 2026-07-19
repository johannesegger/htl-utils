<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { runExecution, type ExecutionState } from '@/execution'
import LabeledInput from './LabeledInput.vue'
import ErrorMessage from './ErrorMessage.vue'
import type { FormDefinition, FormFieldDefinition } from '@/api.ts'

const props = defineProps<{
  name: string
  form: FormDefinition
  showResult: boolean
}>()

const emit = defineEmits<{
  executed: [output: unknown]
  executionError: [message: string]
}>()

type FormField = FormFieldDefinition & { value: string }

type Form = {
  title: string
  fields: FormField[]
}
namespace Form {
  export function empty() {
    return { title: '', fields: [] }
  }
  export function fromDefinition(def: FormDefinition) {
    return {
      title: def.title,
      fields: def.fields.map(v => ({
        ...v,
        value: ''
      }))
    }
  }
}

const executionState = defineModel<ExecutionState>('executionState', { required: true })

const form = ref<Form>(Form.empty())
watch(
  props.form,
  def => {
    form.value = Form.fromDefinition(def)
    executionState.value = { type: 'notExecuted' }
  },
  { immediate: true },
)

function fieldIsValid(field: FormField) {
  return field.inputValidations.every(v => {
    switch (v) {
      case 'notEmpty': return field.value.trim() !== ''
    }
  })
}

const formIsValid = computed(() => {
  if (!form.value) return false
  return form.value.fields.every(field => fieldIsValid(field))
})

async function execute() {
  if (!form.value || !formIsValid.value) return
  const data = Object.fromEntries(form.value.fields.map(v => [v.name, v.value]))
  await runExecution(props.name, data, executionState)
  if (executionState.value.type === 'executed') {
    form.value.fields.forEach(v => v.value = '')
  }
}
</script>

<template>
  <form class="space-y-3" @submit.prevent="execute">
    <LabeledInput v-for="field in form.fields" :key="field.name" :label="field.title ?? field.name">
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

    <button type="submit" class="btn-primary" :disabled="!formIsValid || executionState.type === 'executing'">
      {{ executionState.type === 'executing' ? 'Executing…' : 'Execute' }}
    </button>

    <template v-if="showResult">
      <ErrorMessage v-if="executionState.type === 'executionError'" :message="executionState.message" />
      <pre
        v-if="executionState.type === 'executed'"
        class="rounded bg-gray-900 p-3 text-xs text-gray-100 whitespace-pre overflow-x-auto"
        >{{ executionState.output ? JSON.stringify(executionState.output, null, 2) : 'Execution succeeded' }}</pre>
    </template>
  </form>
</template>
