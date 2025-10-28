<script lang="ts" setup>
import * as Types from './Types'
const model = defineModel<Types.SelectedPersonGroup[][]>()
</script>

<template>
  <div class="flex flex-col gap-8">
    <div v-for="groups in model" class="flex flex-wrap gap-2">
      <button v-for="group in groups" class="btn flex! flex-col"
        :class="{ 'bg-lime-400': group.isSelected === true }"
        :disabled="group.selectablePersons === 0"
        @click="group.isSelected = !group.isSelected">
        <span class="px-2">{{ group.displayName }}</span>
        <span class="text-xs text-black/50">
          <template v-if="group.selectablePersons === group.persons.length">
            {{ group.selectablePersons }}
          </template>
          <template v-else>
            {{ group.selectablePersons }} / {{ group.persons.length }}
          </template>
        </span>
      </button>
    </div>
  </div>
</template>