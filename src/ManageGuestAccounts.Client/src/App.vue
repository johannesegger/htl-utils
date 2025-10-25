<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import ErrorWithRetry from './ErrorWithRetry.vue'
import { uiFetch, type FetchResult } from './UIFetch'
import { tryGetLoggedInUser } from './auth'
import * as DataTransfer from './DataTransfer'
import { pluralize } from './Utils'
import AccountGroupView from './AccountGroupView.vue'

const isLoadingGuestAccounts = ref(false)
const loadGuestAccountsResult = ref<FetchResult>()
const guestAccounts = ref([] as DataTransfer.ExistingAccountGroup[])
const loadGuestAccounts = async () => {
  isLoadingGuestAccounts.value = true
  try {
    guestAccounts.value = []
    loadGuestAccountsResult.value = undefined
    loadGuestAccountsResult.value = await uiFetch('/api/guest-accounts')
    if (loadGuestAccountsResult.value.succeeded) {
      const loadedGroups = await loadGuestAccountsResult.value.response.json() as DataTransfer.ExistingAccountGroup[]
      guestAccounts.value = loadedGroups
    }
  }
  finally {
    isLoadingGuestAccounts.value = false
  }
}
loadGuestAccounts()

const newAccounts = reactive({
  group: '',
  count: 1,
  wlanOnly: true,
  notes: '',
})
const resetNewAccountsForm = () => {
  newAccounts.group = ''
  newAccounts.count = 1
  newAccounts.wlanOnly = true
  newAccounts.notes = ''
}

const printPdf = (base64Content: string) => {
  window.open(`data:application/pdf;base64,${base64Content}`)
}

const monthYearFormat = new Intl.DateTimeFormat('en-US', { month: '2-digit', year: 'numeric' })
const sampleDateNow = monthYearFormat.format(new Date())
const sampleDateFuture = monthYearFormat.format(new Date(new Date().getFullYear(), new Date().getMonth() + 6))
const loggedInUserName = ref<string>()
;(async () => {
  const fullUserName = await tryGetLoggedInUser()
  if (fullUserName === undefined) return
  loggedInUserName.value = fullUserName.replace(/@.*$/, '')
})()
const sampleNotes = computed(() => {
  return `Für FH-Studenten bis ${sampleDateFuture} (${loggedInUserName.value || "EINA"}, ${sampleDateNow})`
})
const setSampleNotes = () => {
  newAccounts.notes = sampleNotes.value
}

const isCreatingGuestAccounts = ref(false)
const createAccountsResponse = ref<DataTransfer.CreateGuestAccountsResponse>()
const createGuestAccounts = async () => {
  isCreatingGuestAccounts.value = true
  try {
    createAccountsResponse.value = undefined
    const dto : DataTransfer.CreateGuestAccountsRequest = {
      group: newAccounts.group,
      count: newAccounts.count,
      wlanOnly: newAccounts.wlanOnly,
      notes: newAccounts.notes,
    }
    const result = await uiFetch('/api/guest-accounts', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(dto)
    })
    if (result.succeeded) {
      const createdAccountGroup = await result.response.json() as DataTransfer.CreatedGuestAccountGroup
      createAccountsResponse.value = { type: 'accounts-created', result: createdAccountGroup }
      if (createdAccountGroup.accounts.every(v => v.errors.length === 0)) {
        resetNewAccountsForm()
      }
      printPdf(createdAccountGroup.pdf)
    }
    else if (result.response?.status === 400) {
      const errorCode = await result.response.text() as DataTransfer.CreateGuestAccountsValidationError
      createAccountsResponse.value = { type: 'validation-error', error: errorCode }
    }
    else if (result.response?.status === 403) {
      createAccountsResponse.value = { type: 'not-authorized' }
    }
    else {
      createAccountsResponse.value = { type: 'other-error' }
    }
  }
  finally {
    isCreatingGuestAccounts.value = false
  }
}
</script>

<template>
  <header class="px-8 py-6 bg-indigo-700 text-slate-200">
    <h1 class="text-3xl">Gästeaccounts verwalten</h1>
  </header>
  <main class="flex flex-col gap-4 px-8 py-6">
    <section class="px-4 py-2 border border-slate-500 rounded">
      <h2 class="text-xl mb-2">Neue Gästeaccounts anlegen</h2>
      <form @submit.prevent="createGuestAccounts">
        <fieldset class="flex flex-col gap-4" :disabled="isCreatingGuestAccounts">
          <div class="flex flex-col items-start gap-1">
            <span class="text-sm">Gruppe (z.B. "fh", "lego"):</span>
            <input type="text" v-model="newAccounts.group" pattern="^[a-z0-9]{1,8}$" required maxlength="8" class="input-text" />
            <span class="text-xs">nur Kleinbuchstaben und Ziffern, keine Umlaute, max. 8 Zeichen</span>
          </div>
          <div class="flex flex-col items-start gap-1">
            <span class="text-sm">Anzahl:</span>
            <input type="number" v-model="newAccounts.count" min="1" class="input-text" />
            <span class="text-xs">kann zu bestehender Gruppe hinzugefügt werden</span>
          </div>
          <div class="flex flex-col items-start gap-1">
            <span class="text-sm">Notizen:</span>
            <input type="text" v-model="newAccounts.notes" class="input-text w-full max-w-100" />
            <span class="text-xs select-none" @dblclick="setSampleNotes">z.B. "{{ sampleNotes }}"</span>
          </div>
          <label class="self-start flex items-center gap-2">
            <input type="checkbox" v-model="newAccounts.wlanOnly" />
            <span class="text-sm">Nur WLAN-Zugriff</span>
          </label>
          <div class="flex flex-col items-start gap-1">
            <button type="submit" class="btn text-green-600 border-green-600">Accounts anlegen und drucken</button>
            <template v-if="createAccountsResponse !== undefined">
              <span v-if="createAccountsResponse.type === 'not-authorized'" class="text-sm text-red-700">Sie sind nicht berechtigt.</span>
              <template v-if="createAccountsResponse.type === 'validation-error'">
                <span v-if="createAccountsResponse.error === 'InvalidGroupName'" class="text-sm text-red-700">Ungültiger Gruppenname.</span>
                <span v-else-if="createAccountsResponse.error === 'InvalidSize'" class="text-sm text-red-700">Ungültige Accountanzahl.</span>
                <span v-else class="text-sm text-red-700">Fehler beim Anlegen. Bitte überprüfen Sie Ihre Eingaben.</span>
              </template>
              <template v-else-if="createAccountsResponse.type === 'accounts-created'">
                <span v-if="createAccountsResponse.result.accounts.some(v => v.errors.length > 0)" class="text-sm text-red-700">Beim Anlegen eines oder mehrerer Accounts sind Fehler aufgetreten.</span>
                <span v-else class="text-sm text-green-600">{{ pluralize(createAccountsResponse.result.accounts.length, 'Account wurde', 'Accounts wurden') }} erfolgreich angelegt.</span>
              </template>
              <span v-else-if="createAccountsResponse.type === 'other-error'" class="text-sm text-red-700">Beim Anlegen der Accounts ist ein Fehler aufgetreten. Bitte prüfen Sie, ob Accounts angelegt wurden und entfernen Sie sie ggf. wieder.</span>
            </template>
          </div>
        </fieldset>
      </form>
    </section>

    <section class="px-4 py-2 border border-slate-500 rounded">
      <div class="flex items-center gap-2 mb-2">
        <button class="btn" title="Aktualisieren" :disabled="isLoadingGuestAccounts" @click="loadGuestAccounts">
          <i class="fa-solid fa-rotate" :class="{ 'animate-spin': isLoadingGuestAccounts }"></i>
        </button>
        <h2 class="text-xl">Aktive Gästeaccounts</h2>
      </div>
      <template v-if="loadGuestAccountsResult !== undefined">
        <ErrorWithRetry v-if="loadGuestAccountsResult.succeeded === false && loadGuestAccountsResult.response?.status === 403" @retry="loadGuestAccounts">Fehler beim Laden der Gästeaccounts. Sie sind nicht berechtigt.</ErrorWithRetry>
        <ErrorWithRetry v-else-if="loadGuestAccountsResult.succeeded === false" @retry="loadGuestAccounts">Fehler beim Laden der Gästeaccounts.</ErrorWithRetry>
        <div v-else-if="guestAccounts.length === 0" class="text-sm">Keine Gästeaccounts vorhanden.</div>
        <div v-else class="flex flex-col gap-4">
          <AccountGroupView v-for="accountGroup in guestAccounts" :key="accountGroup.group" :account-group="accountGroup" />
        </div>
      </template>
    </section>
  </main>
</template>
