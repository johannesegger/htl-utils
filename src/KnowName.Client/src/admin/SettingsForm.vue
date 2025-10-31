<script lang="ts" setup>
import { computed, reactive, ref, watch } from 'vue'
import { Result, uiFetch, Workflow, type WorkflowError } from '@/UIFetch'
import * as DataTransfer from './DataTransfer'
import { bytesToBase64 } from '@/Utils'
import ErrorWithRetry from '@/ErrorWithRetry.vue'

type Settings = {
  sokrates: {
    webServiceUrl: string
    schoolId: string
    userName: string
    password: string
    showPassword: boolean
    existingCertificate: {
      subject: string
      issuer: string
      validFrom: Date
      validUntil: Date
    } | undefined
    newCertificate: File | undefined
    newCertificatePassphrase: string
  }
}
namespace Settings {
  export const fromDto = (v: DataTransfer.ExistingSettings) : Settings => {
    return {
      sokrates: {
        webServiceUrl: v.sokrates?.webServiceUrl || '',
        schoolId: v.sokrates?.schoolId || '',
        userName: v.sokrates?.userName || '',
        password: v.sokrates?.password || '',
        showPassword: false,
        existingCertificate:
          v.sokrates != null ?
            {
              subject: v.sokrates.clientCertificate.subject,
              issuer: v.sokrates.clientCertificate.issuer,
              validFrom: new Date(v.sokrates.clientCertificate.validFrom),
              validUntil: new Date(v.sokrates.clientCertificate.validUntil),
            } : undefined,
        newCertificate: undefined,
        newCertificatePassphrase: '',
      }
    }
  }
}

const loadedSettings = ref<Settings>()

type LoadSettingsError = 'not-authorized'
const loadSettingsWorkflow = Workflow.init<[], DataTransfer.ExistingSettings, LoadSettingsError>(async () => {
  const response = await uiFetch('/api/admin/settings')
  if (response.ok) {
    return Result.ok(await response.json())
  }
  else if (response.status === 403) {
    return Result.error('not-authorized')
  }
  else {
    throw response
  }
})
loadSettingsWorkflow.run()

watch(loadSettingsWorkflow.result, v => {
  if (v?.succeeded !== true) return

  loadedSettings.value = Settings.fromDto(v.result)
}, { deep: true })

const setSokratesCertFile = (e: Event) => {
  const fileList = (e.target as HTMLInputElement).files
  if (fileList === null || loadedSettings.value === undefined) return

  loadedSettings.value.sokrates.newCertificate = fileList[0]
}

const formatDateTime = (date: Date) => {
  const format = new Intl.DateTimeFormat('de-AT', { weekday: 'short', day: '2-digit', 'month': '2-digit', 'year': 'numeric', hour: '2-digit', minute: '2-digit' })
  return format.format(date)
}

const saveSettingsWorkflow = Workflow.init<[], DataTransfer.ExistingSettings, DataTransfer.SaveSettingsError[]>(async () => {
  if (loadedSettings.value === undefined) throw 'Can\'t save settings when settings are not loaded'

  const newSettings : DataTransfer.NewSettings = {
    sokrates: {
      webServiceUrl: loadedSettings.value.sokrates.webServiceUrl,
      schoolId: loadedSettings.value.sokrates.schoolId,
      userName: loadedSettings.value.sokrates.userName,
      password: loadedSettings.value.sokrates.password,
      clientCertificate: loadedSettings.value.sokrates.newCertificate !== undefined ? bytesToBase64(await loadedSettings.value.sokrates.newCertificate?.bytes()) : undefined,
      clientCertificatePassphrase: loadedSettings.value.sokrates.newCertificatePassphrase,
    }
  }
  const response = await uiFetch('/api/admin/settings', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(newSettings)
  })
  if (response.ok) {
    return Result.ok(await response.json())
  }
  else if (response.status === 400) {
    return Result.error(await response.json())
  }
  else {
    throw response
  }
})

const saveSettingsErrorMessages = computed(() => {
  if (saveSettingsWorkflow.result.value?.succeeded !== false) return []

  switch (saveSettingsWorkflow.result.value.error.type) {
    case 'unexpected': return ['Unerwarteter Fehler beim Speichern der Einstellungen.']
    case 'expected': return saveSettingsWorkflow.result.value.error.error.map(e => {
      switch (e) {
        case 'invalid-sokrates-certificate': return 'Ungültiges Zertifikat oder Zertifikat-Passwort.'
        case 'incomplete-config': return 'Die Einstellungen wurden nicht gespeichert, weil die Konfiguration nicht vollständig wäre.'
      }
    })
  }
})

watch(saveSettingsWorkflow.result, v => {
  if (v?.succeeded !== true) return

  loadedSettings.value = Settings.fromDto(v.result)
}, { deep: true })
</script>

<template>
  <div v-if="loadedSettings === undefined">Einstellungen werden geladen...</div>
  <ErrorWithRetry v-else-if="loadSettingsWorkflow.result.value?.succeeded === false" @retry="loadSettingsWorkflow.run">Fehler beim Laden der Einstellungen</ErrorWithRetry>
  <div v-else>
    <form @submit.prevent="saveSettingsWorkflow.run">
      <fieldset class="flex flex-col gap-4" :disabled="saveSettingsWorkflow.isRunning.value === true">
        <h2 class="text-xl small-caps">Einstellungen</h2>
        <div class="flex flex-col items-start gap-1">
          <span class="text-sm opacity-75">Sokrates Web-Service-URL:</span>
          <input type="text" v-model="loadedSettings.sokrates.webServiceUrl" required class="input-text w-full max-w-96" />
        </div>
        <div class="flex flex-col items-start gap-1">
          <span class="text-sm opacity-75">Sokrates Schulkennzahl:</span>
          <input type="text" v-model="loadedSettings.sokrates.schoolId" required class="input-text" />
        </div>
        <div class="flex flex-col items-start gap-1">
          <span class="text-sm opacity-75">Sokrates Benutzername:</span>
          <input type="text" v-model="loadedSettings.sokrates.userName" required class="input-text" />
        </div>
        <div class="flex flex-col items-start gap-1">
          <span class="text-sm opacity-75">Sokrates Passwort:</span>
          <div class="flex items-center gap-2">
            <input :type="loadedSettings.sokrates.showPassword ? 'text' : 'password'" v-model="loadedSettings.sokrates.password" required class="input-text" />
            <a class="text-sm text-blue-700 underline cursor-pointer" @click="loadedSettings.sokrates.showPassword = !loadedSettings.sokrates.showPassword">{{ loadedSettings.sokrates.showPassword ? 'ausblenden' : 'anzeigen' }}</a>
          </div>
        </div>
        <div class="flex flex-col items-start gap-1">
          <span class="text-sm opacity-75">Neues Sokrates Client-Zertifikat:</span>
          <label class="flex items-center gap-2">
            <input class="hidden" type="file" v-on:change="setSokratesCertFile" />
            <a class="btn">Zertifikat hochladen</a>
            <span v-if="loadedSettings.sokrates.newCertificate !== undefined">{{ loadedSettings.sokrates.newCertificate.name }}</span>
          </label>
          <span v-if="loadedSettings.sokrates.existingCertificate === undefined" class="text-sm">
            Kein Zertifikat vorhanden.
          </span>
          <span v-else class="text-sm">
            Aktuelles Zertifikat:
            <ul class="list-disc ml-4">
              <li>Ausgestellt für: {{ loadedSettings.sokrates.existingCertificate.subject }}</li>
              <li>Aussteller: {{ loadedSettings.sokrates.existingCertificate.issuer }}</li>
              <li>Gültig von: {{ formatDateTime(new Date(loadedSettings.sokrates.existingCertificate.validFrom)) }}</li>
              <li>Gültig bis: {{ formatDateTime(new Date(loadedSettings.sokrates.existingCertificate.validUntil)) }}</li>
            </ul>
          </span>
        </div>
        <div class="flex flex-col items-start gap-1">
          <span class="text-sm opacity-75">Passwort für neues Sokrates Client-Zertifikat:</span>
          <input type="password" v-model="loadedSettings.sokrates.newCertificatePassphrase" class="input-text" />
        </div>
        <div class="flex flex-col items-start gap-2">
          <button type="submit" class="btn text-green-600 border-green-600">Einstellungen speichern</button>
          <template v-if="saveSettingsWorkflow.isRunning.value === false">
            <ul v-if="saveSettingsErrorMessages.length > 0" class="list-disc ml-4">
              <li v-for="message in saveSettingsErrorMessages" class="text-red-500 text-sm">{{ message }}</li>
            </ul>
            <span v-if="saveSettingsWorkflow.result.value?.succeeded === true" class="text-sm text-green-500">
              Die Einstellungen wurden erfolgreich gespeichert.
            </span>
          </template>
        </div>
      </fieldset>
    </form>
  </div>
</template>