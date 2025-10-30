<script lang="ts" setup>
import ErrorWithRetry from './ErrorWithRetry.vue'
import { Result, uiFetch, Workflow, type WorkflowError } from './UIFetch'
import * as DataTransfer from './DataTransfer.Admin'
import PersonGroupOverview from './PersonGroupOverview.vue'
import PhotoUpload from './PhotoUpload.vue'

type LoadAdminOverviewError = 'not-authorized'
const loadAdminOverviewWorkflow = Workflow.init<[], DataTransfer.Settings, LoadAdminOverviewError>(async () => {
  const response = await uiFetch('/api/admin')
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
loadAdminOverviewWorkflow.run()

const getErrorMessage = (error: WorkflowError<LoadAdminOverviewError>) => {
  switch (error.type) {
    case 'unexpected': return 'Unerwarteter Fehler beim Laden der Einstellungen.'
    case 'expected':
      switch (error.error) {
        case 'not-authorized': return 'Fehler beim Laden der Einstellungen. Sie sind nicht berechtigt.'
      }
  }
}
</script>

<template>
  <header class="flex flex-cols items-center p-8 bg-red-200">
    <div class="justify-self-start flex items-center gap-2">
      <img src="/logo.svg" width="32px" height="32px" />
      <h1 class="text-3xl small-caps">Know Name - Administration</h1>
    </div>
  </header>
  <main class="flex flex-col gap-8 p-4">
    <PhotoUpload :disabled="loadAdminOverviewWorkflow.isRunning.value === true" @uploaded="loadAdminOverviewWorkflow.run" />

    <div v-if="loadAdminOverviewWorkflow.isRunning.value === true">Einstellungen werden geladen...</div>
    <ErrorWithRetry v-else-if="loadAdminOverviewWorkflow.result.value?.succeeded === false" @retry="loadAdminOverviewWorkflow.run">{{ getErrorMessage(loadAdminOverviewWorkflow.result.value.error) }}</ErrorWithRetry>
    <PersonGroupOverview v-else-if="loadAdminOverviewWorkflow.result.value?.succeeded === true" v-for="group in loadAdminOverviewWorkflow.result.value.result.personGroups" :key="group.displayName"
      :group="group" />
  </main>
</template>