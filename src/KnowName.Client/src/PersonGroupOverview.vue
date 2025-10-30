<script lang="ts" setup>
import { computed, ref } from 'vue'
import * as DataTransfer from './DataTransfer.Admin'
import PersonListItem from './PersonListItem.vue'

const props = defineProps<{
  group: DataTransfer.PersonGroup
}>()

const personsWithPhoto = computed(() => props.group.persons.filter(v => v.imageUrl !== null))
const personsWithPhotoRatioClass = computed(() => {
  if (props.group.persons.length === 0) return "text-black"

  const ratio = personsWithPhoto.value.length / props.group.persons.length
  if (ratio === 1) return "text-green-500"
  if (ratio === 0) return "text-red-500"
  return "text-yellow-500"
})

const showImages = ref(false)
</script>

<template>
  <div class="flex flex-col gap-2">
    <div class="flex items-baseline gap-2">
      <h2 class="text-xl">{{ group.displayName }}</h2>
      <span class="text-sm" :class="personsWithPhotoRatioClass">{{ personsWithPhoto.length }}/{{ group.persons.length }} Fotos</span>
      <label class="flex items-center gap-2">
        <input type="checkbox" v-model="showImages" />
        <span>Fotos anzeigen</span>
      </label>
    </div>
    <div class="flex flex-wrap gap-2">
      <PersonListItem v-for="person in group.persons" :key="person.displayName"
        :person="person"
        :show-image="showImages"
        class="basis-80" />
    </div>
  </div>
</template>