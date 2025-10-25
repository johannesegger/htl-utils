<script setup lang="ts">
defineEmits(['retry'])

const props = withDefaults(
  defineProps<{
    retryTitle?: string
    type?: 'inline' | 'banner'
  }>(), {
    type: 'banner'
  }
)
const containerClasses = props.type === 'banner'
  ? [ "flex", "justify-center", "items-center", "gap-2", "bg-red-800", "p-4", "rounded-sm", "text-white" ]
  : [ "inline-flex", "justify-center", "items-center", "gap-2", "rounded-sm", "text-red-800" ]

const buttonClasses = props.type === 'banner'
  ? [ "btn", "text-white", "hover:bg-white", "hover:text-red-800" ]
  : [ "btn", "text-red-800", "hover:bg-red-800", "hover:text-white" ]
</script>

<template>
  <div :class="containerClasses">
    <div class="font-semibold"><slot /></div>
    <button :class="buttonClasses" @click="$emit('retry')">{{ retryTitle ?? 'Erneut versuchen' }}</button>
  </div>
</template>
