<script setup lang="ts">
const model = defineModel<File>()

const selectedFileChanged = (e: Event) => {
  model.value = (e.target as HTMLInputElement)?.files?.[0]
}

defineProps<{
  title: string,
  fileTypes: string[]
}>()
</script>

<template>
  <label class="flex flex-col">
    <span class="input-label">{{ title }}</span>
    <input class="hidden" type="file" :accept="fileTypes.join(',')" required @change="selectedFileChanged" />
    <div class="flex gap-4 items-center">
      <div class="btn btn-blue">Auswählen</div>
      <span v-if="modelValue">{{ modelValue.name }}</span>
    </div>
  </label>
</template>
