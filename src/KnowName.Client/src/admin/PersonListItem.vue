<script lang="ts" setup>
import { ref, watch } from 'vue'
import { PersonImage } from '@/PersonImage'
import type { Person } from './DataTransfer'

const props = defineProps<{
  person: Person
  showImage: boolean
}>()

const image = ref(PersonImage.fromLink(props.person.imageUrl))

watch(() => props.showImage, async showImage => {
  if (showImage === false) return

  image.value = await PersonImage.load(image.value, { height: 75 })
})
</script>

<template>
  <div class="flex items-center gap-2">
    <img v-if="showImage && image?.type === 'blob'" :src="image.url" />
    <span :class="{ 'text-green-500': image !== undefined, 'text-red-500': image === undefined }">{{ person.displayName }}</span>
  </div>
</template>