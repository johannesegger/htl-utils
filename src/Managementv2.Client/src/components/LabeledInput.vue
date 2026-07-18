<script setup lang="ts">
defineProps<{
  label: string
}>()
</script>

<template>
  <div class="flex flex-col">
    <span class="text-xs text-gray-500">{{ label }}</span>
    <slot />
  </div>
</template>