<script setup lang="ts">
defineProps<{
  message: string | null
}>()
</script>

<template>
  <p v-if="message" class="rounded bg-red-100 px-3 py-2 text-sm text-red-800 whitespace-pre overflow-x-auto">{{ message }}</p>
</template>