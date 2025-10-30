<script lang="ts" setup>
import { Result, uiFetch, Workflow } from './UIFetch'
import * as DataTransfer from './DataTransfer.Admin'
import { pluralize } from './Utils'
import { computed, watch } from 'vue'

const props = defineProps<{
  disabled: boolean
}>()

const emit = defineEmits<{
  uploaded: []
}>()

const uploadFilesWorkflow = Workflow.init<[File[]], DataTransfer.UploadPhotosResult, never>(async (files: File[]) => {
  const formData = new FormData()
  files.forEach(file => formData.append('files', file))
  const response = await uiFetch('/api/admin/photos', {
    method: 'POST',
    body: formData,
  })
  if (response.ok) {
    return Result.ok(await response.json())
  }
  else {
    throw response
  }
})

const disabled = computed(() => props.disabled === true || uploadFilesWorkflow.isRunning.value === true)

const uploadFiles = async (e: Event) => {
  const fileList = (e.target as HTMLInputElement).files
  if (fileList === null) return

  await uploadFilesWorkflow.run([...fileList])
}

watch(() => uploadFilesWorkflow.result.value?.succeeded, uploadSucceeded => {
  if (uploadSucceeded === true) {
    emit('uploaded')
  }
})
</script>

<template>
  <div class="flex flex-col gap-2">
    <div class="flex items-center gap-2">
      <label>
        <input class="hidden" type="file" multiple :disabled="disabled === true" v-on:change="uploadFiles" />
        <a class="btn" :class="{ 'opacity-50 cursor-not-allowed!': disabled === true }">Fotos hochladen und Archiv bereinigen</a>
      </label>
      <span v-if="uploadFilesWorkflow.isRunning.value === true">Fotos werden hochgeladen...</span>
    </div>
    <template v-if="uploadFilesWorkflow.isRunning.value === false">
      <span v-if="uploadFilesWorkflow.result.value?.succeeded === false" class="text-red-500">Fehler beim Hochladen der Fotos.</span>
      <div v-else-if="uploadFilesWorkflow.result.value?.succeeded === true" class="flex flex-col gap-1">
        <span>{{ pluralize(uploadFilesWorkflow.result.value.result.updatedTeacherPhotos.length, 'Lehrerfoto wurde', 'Lehrerfotos wurden') }} aktualisiert.</span>
        <span>{{ pluralize(uploadFilesWorkflow.result.value.result.updatedStudentPhotos.length, 'Schülerfoto wurde', 'Schülerfotos wurden') }} aktualisiert.</span>
      </div>
    </template>
  </div>
</template>