<script setup lang="ts">
import { ref, watch } from 'vue'
import { uiFetch } from './UIFetch'
import type { TestData } from './TestData'
import type { StudentIdentifierDto } from './DataSync';

const props = defineProps<{
  tests: TestData[]
}>()

const store = (key: string, value: string | undefined) => {
  if (value === undefined) {
    localStorage.removeItem(key)
  }
  else {
    localStorage.setItem(key, value)
  }
}

const letterText = ref(localStorage.getItem('student-letter-text') || undefined)
watch(letterText, v => store('student-letter-text', v))

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

const mailSubject = ref(localStorage.getItem('student-letter-mail-subject') || undefined)
watch(mailSubject, v => store('student-letter-mail-subject', v))
const mailText = ref(localStorage.getItem('student-letter-mail-text') || undefined)
watch(mailText, v => store('student-letter-mail-text', v))

const isSendingLetters = ref(false)
const hasSendingLettersFailed = ref(false)
type SendLetterError =
  { type: 'sending-mail-failed', studentMailAddress: string } |
  { type: 'student-has-no-mail-address', student: StudentIdentifierDto }
const sendLetterErrors = ref([] as SendLetterError[])
const sendingLettersSucceeded = ref(false)
const sendLetters = async () => {
  sendLetterErrors.value = []
  sendingLettersSucceeded.value = false
  const result = await uiFetch(isSendingLetters, hasSendingLettersFailed, '/api/letter/students', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ tests: props.tests, letterText: letterText.value, mailSubject: mailSubject.value, mailText: mailText.value })
  })
  if (result.succeeded) {
    sendingLettersSucceeded.value = true
  }
  else if (result.response !== undefined) {
    sendLetterErrors.value = await result.response.json()
  }
}

const getStudentName = (v: StudentIdentifierDto) => {
  if ('fullName' in v) {
    return `${v.fullName} (${v.className})`
  }
  else {
    return `${v.lastName} ${v.firstName} (${v.className})`
  }
}
</script>

<template>
  <div class="flex flex-col gap-2">
    <div class="grid grid-cols-2 gap-4">
      <div class="flex flex-col gap-2">
        <div class="flex flex-col">
          <span class="input-label">Brieftext</span>
          <textarea v-model="letterText" class="input-text" rows="10"></textarea>
        </div>

        <div class="flex items-center gap-2">
          <button class="btn" :disabled="tests.length === 0 || isGeneratingLetters" @click="generateLetters">Briefe erzeugen</button>
          <span v-if="hasGeneratingLettersFailed" class="text-red-800">Fehler beim Erzeugen der Briefe.</span>
        </div>
      </div>

      <div class="flex flex-col gap-2">
        <div class="flex flex-col">
          <span class="input-label">Mailbetreff</span>
          <input v-model="mailSubject" class="input-text" />
        </div>

        <div class="flex flex-col">
          <span class="input-label">Mailinhalt</span>
          <textarea v-model="mailText" class="input-text" rows="7"></textarea>
        </div>

        <div class="flex items-center gap-2">
          <button class="btn text-red-800" :disabled="tests.length === 0 || isSendingLetters" @click="sendLetters">Briefe per Mail versenden</button>
          <span v-if="hasSendingLettersFailed" class="text-red-800">Fehler beim Versenden der Briefe.</span>
          <span v-else-if="sendingLettersSucceeded" class="text-green-500">Alle Schülerbriefe wurden erfolgreich versendet.</span>
        </div>

        <ul v-if="sendLetterErrors" class="list-disc ml-4">
          <li v-for="error in sendLetterErrors" :key="JSON.stringify(error)" class="text-red-800">
            <span v-if="error.type === 'sending-mail-failed'">Fehler beim Senden der Mail an {{ error.studentMailAddress }}.</span>
            <span v-else-if="error.type === 'student-has-no-mail-address'">Mail-Adresse von {{ getStudentName(error.student) }} wurde nicht gefunden.</span>
          </li>
        </ul>
      </div>
    </div>

    <iframe v-if="pdfObjectUrl !== undefined" :src="pdfObjectUrl" class="w-[210mm] h-[297mm]"></iframe>
  </div>
</template>