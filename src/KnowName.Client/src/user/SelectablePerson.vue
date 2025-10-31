<script lang="ts" setup>
import { useTemplateRef, watch } from 'vue';

const props = defineProps<{
  displayName: string
  groupName: string
  isSelectable: boolean
  isSelected: boolean
}>()

defineEmits<{
  select: []
}>()

const node = useTemplateRef('node')

watch(() => props.isSelected, isSelected => {
  if (isSelected === false) return

  node.value?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
})
</script>

<template>
  <div ref="node">
    <div v-if="isSelectable === false" class="px-4 py-2 text-sm rounded opacity-25 cursor-not-allowed select-none">
      {{ displayName }} ({{ groupName }})
    </div>
    <div v-else class="px-4 py-2 text-sm rounded opacity-75 cursor-pointer hover:bg-indigo-200 select-none"
      :class="{ 'selected bg-indigo-400': isSelected }"
      @click="$emit('select')">
      {{ displayName }} ({{ groupName }})
    </div>
  </div>
</template>