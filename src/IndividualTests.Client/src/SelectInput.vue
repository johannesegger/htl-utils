<script setup lang="ts">
const model = defineModel<string | undefined>()

defineProps<{
  emptyText: string | undefined
  options: { value: string, text: string }[]
}>()
</script>

<template>
  <select class="input-select" v-model="model">
    <option v-if="emptyText !== undefined" :value="undefined">{{ emptyText }}</option>
    <option v-for="o in options" :key="o.value" :value="o.value">{{ o.text }}</option>
  </select>
</template>