<script setup lang="ts">
import ErrorWithRetry from '@/ErrorWithRetry.vue'
import PhotoUpload from './PhotoUpload.vue'
import PersonGroupOverview from './PersonGroupOverview.vue'
import { Result, uiFetch, Workflow, type WorkflowError } from '@/UIFetch'
import * as DataTransfer from './DataTransfer'

type LoadPersonsError = 'not-authorized'
const loadPersonsWorkflow = Workflow.init<[], DataTransfer.PersonGroup[], LoadPersonsError>(async () => {
  const response = await uiFetch('/api/admin/persons')
  if (response.ok) {
    return Result.ok(await response.json())
  }
  else if (response.status === 403) {
    return Result.error('not-authorized')
  }
  else {
    throw response
  }
})
loadPersonsWorkflow.run()

const getErrorMessage = (error: WorkflowError<LoadPersonsError>) => {
  switch (error.type) {
    case 'unexpected': return 'Unerwarteter Fehler beim Laden der Personen.'
    case 'expected':
      switch (error.error) {
        case 'not-authorized': return 'Fehler beim Laden der Personen. Sie sind nicht berechtigt.'
      }
  }
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <h2 class="text-lg small-caps">Fotos aktualisieren</h2>
    <PhotoUpload :disabled="loadPersonsWorkflow.isRunning.value === true" @uploaded="loadPersonsWorkflow.run" />

    <div v-if="loadPersonsWorkflow.isRunning.value === true">Personendaten werden geladen...</div>
    <ErrorWithRetry v-else-if="loadPersonsWorkflow.result.value?.succeeded === false" @retry="loadPersonsWorkflow.run">{{ getErrorMessage(loadPersonsWorkflow.result.value.error) }}</ErrorWithRetry>
    <PersonGroupOverview v-else-if="loadPersonsWorkflow.result.value?.succeeded === true" v-for="group in loadPersonsWorkflow.result.value.result" :key="group.displayName"
      :group="group" />
  </div>
</template>