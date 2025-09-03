<script setup lang="ts">
import { ref, watch } from 'vue'
import { uiFetch } from './UIFetch'
import type { TestData } from './TestData'

const props = defineProps<{
  tests: TestData[]
}>()

const letterText = ref(localStorage.getItem('student-letter-text') || undefined)
watch(letterText, v => {
  if (v === undefined) {
    localStorage.removeItem('student-letter-text')
  }
  else {
    localStorage.setItem('student-letter-text', v)
  }
})

const isGeneratingLetters = ref(false)
const hasGeneratingLettersFailed = ref(false)
const pdfObjectUrl = ref<string>()
const generateLetters = async () => {
  const result = await uiFetch(isGeneratingLetters, hasGeneratingLettersFailed, '/api/letter/students', {
    method: 'QUERY',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ tests: props.tests, letterText: letterText.value })
  })
  if (result.succeeded) {
    const pdf = await result.response.blob()
    pdfObjectUrl.value = URL.createObjectURL(pdf)
  }
  else {
    pdfObjectUrl.value = undefined
  }
}
</script>

<template>
  <div class="flex flex-col gap-2">
    <div class="flex flex-col">
      <span class="input-label">Brieftext</span>
      <textarea v-model="letterText" class="input-text" rows="10"></textarea>
    </div>

    <div class="flex items-center gap-2">
      <button class="btn" :disabled="tests.length === 0 || isGeneratingLetters" @click="generateLetters">Briefe erzeugen</button>
      <span v-if="hasGeneratingLettersFailed" class="text-red-800">Fehler beim Erzeugen der Briefe.</span>
    </div>

    <iframe v-if="pdfObjectUrl !== undefined" :src="pdfObjectUrl" class="w-[210mm] h-[297mm]"></iframe>
  </div>
</template>