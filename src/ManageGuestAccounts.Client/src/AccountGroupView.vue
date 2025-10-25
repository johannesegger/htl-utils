<script lang="ts" setup>
import { computed, ref } from 'vue'
import * as DataTransfer from './DataTransfer'
import { uiFetch } from './UIFetch'
import { pluralize } from './Utils'

const props = defineProps<{
  accountGroup: DataTransfer.ExistingAccountGroup
}>()

const formatAccountCreatedTimestamp = (date: Date) => {
  const format = new Intl.DateTimeFormat('de-AT', { weekday: 'short', day: '2-digit', 'month': '2-digit', 'year': 'numeric', hour: '2-digit', minute: '2-digit' })
  return format.format(date)
}

type RemoveState = 'none' | 'is-marked-for-removal' | 'is-removing' | 'has-removing-failed' | 'is-removed'
const removeState = ref<RemoveState>('none')
const removeGroup = async () => {
  if (removeState.value === 'none') {
    removeState.value = 'is-marked-for-removal'
    return
  }
  removeState.value = 'is-removing'
  try {
    const result = await uiFetch(`/api/guest-accounts/${props.accountGroup.group}`, {
      method: 'DELETE'
    })
    removeState.value = result.succeeded ? 'is-removed' : 'has-removing-failed'
  }
  catch {
    removeState.value = 'has-removing-failed'
  }
}

const removeButtonTitle = computed(() => {
  switch(removeState.value) {
    case 'none': return 'Gruppe entfernen'
    case 'is-marked-for-removal': return 'Gruppe wirklich entfernen?'
    case 'is-removing': return 'Gruppe wird entfernt.'
    case 'has-removing-failed': return 'Gruppe wirklich entfernen?'
    case 'is-removed': return 'Gruppe wurde erfolgreich entfernt.'
  }
})
</script>

<template>
  <div :class="{ 'opacity-50': removeState === 'is-removed' }">
    <div class="flex items-center gap-4">
      <span>Gruppe "{{ accountGroup.group }}" ({{ pluralize(accountGroup.accounts.length, 'Account', 'Accounts') }})</span>
      <button class="btn"
        :class="{ 'text-red-700 border-red-700': removeState === 'is-marked-for-removal' }"
        :title="removeButtonTitle"
        :disabled="removeState === 'is-removing' || removeState === 'is-removed'"
        @click="removeGroup">
        <i class="fa-solid fa-trash-can"></i>
      </button>
      <span v-if="removeState === 'has-removing-failed'" class="text-sm text-red-700">Fehler beim Entfernen der Gruppe.</span>
    </div>
    <ul class="ml-4 list-disc text-sm">
      <li v-for="account in accountGroup.accounts" :key="account.name">
        {{ account.name }}
        <ul class="ml-4 list-[circle] text-xs">
          <li>angelegt am {{ formatAccountCreatedTimestamp(new Date(account.createdAt)) }}</li>
          <li v-if="account.wlanOnly">Nur WLAN-Zugriff</li>
          <li>{{ account.notes !== undefined ? account.notes : 'Keine Notizen vorhanden' }}</li>
        </ul>
      </li>
    </ul>
  </div>
</template>