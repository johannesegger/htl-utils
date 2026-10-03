<script setup lang="ts">
import { ref, watchEffect } from 'vue'
import type { ExecutionOutput } from '@/api'

const props = defineProps<{
  output: ExecutionOutput
  /** Shown when the script produced no output at all. */
  emptyMessage?: string
}>()

// A file arrives as base64 in the response, so the download link points at a blob built
// here. The cleanup releases it again when the result changes or the component goes
// away: a calculated operation holds one result per row, and each would otherwise leak
// until the page is reloaded.
const downloadUrl = ref<string | null>(null)

watchEffect((onCleanup) => {
  if (props.output.kind !== 'file') {
    downloadUrl.value = null
    return
  }

  const bytes = Uint8Array.from(atob(props.output.content), (c) => c.charCodeAt(0))
  const url = URL.createObjectURL(new Blob([bytes], { type: props.output.contentType }))
  downloadUrl.value = url
  onCleanup(() => URL.revokeObjectURL(url))
})
</script>

<template>
  <div>
    <a v-if="output.kind === 'file' && downloadUrl"
      class="btn-secondary inline-block self-start"
      :href="downloadUrl"
      :download="output.name"
      >Download {{ output.name }}</a>
    <pre v-else-if="output.kind === 'text'"
      class="rounded bg-gray-900 p-3 text-xs text-gray-100 whitespace-pre overflow-x-auto"
      >{{ output.text }}</pre>
    <pre v-else-if="output.kind === 'json'"
      class="rounded bg-gray-900 p-3 text-xs text-gray-100 whitespace-pre overflow-x-auto"
      >{{ JSON.stringify(output.data, null, 2) }}</pre>
    <pre v-else
      class="rounded bg-gray-900 p-3 text-xs text-gray-100 whitespace-pre overflow-x-auto"
      >{{ emptyMessage ?? 'Execution succeeded' }}</pre>
  </div>
</template>
