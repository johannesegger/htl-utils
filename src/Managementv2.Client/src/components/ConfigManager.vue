<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api } from '@/api'
import {
  configKindLabels,
  emptyEntry,
  fileToBase64,
  fromDto,
  toDto,
  type ConfigEntry,
  type ConfigKind,
} from '@/config'
import LabeledInput from './LabeledInput.vue'
import ErrorMessage from './ErrorMessage.vue'

type LoadState =
  { type: 'notLoaded' } |
  { type: 'loading' } |
  { type: 'loadError', message: string } |
  {
    type: 'loaded',
    entries: ConfigEntry[],
    saveState:
      { type: 'notSaved' } |
      { type: 'saving' } |
      { type: 'saveError', message: string } |
      { type: 'saved' }
  }

const loadState = ref<LoadState>({ type: 'notLoaded' })

// const entries = ref<ConfigEntry[]>([])
// const loading = ref(false)
// const saving = ref(false)
// const error = ref<string | null>(null)
// const message = ref<string | null>(null)

const kinds = Object.keys(configKindLabels) as ConfigKind[]

async function load() {
  loadState.value = { type: 'loading' }
  try {
    const entries = fromDto(await api.getConfig())
    loadState.value = { type: 'loaded', entries: entries, saveState: { type: 'notSaved' } }
  } catch (e) {
    loadState.value = { type: 'loadError', message: (e as Error).message }
  }
}

function addEntry() {
  if (loadState.value.type !== 'loaded') return

  loadState.value.entries.push(emptyEntry())
}

function removeEntry(index: number) {
  if (loadState.value.type !== 'loaded') return
  
  loadState.value.entries.splice(index, 1)
}

async function onFile(entry: ConfigEntry, event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  entry.fileBase64 = await fileToBase64(file)
  entry.fileName = file.name
}

async function save() {
  if (loadState.value.type !== 'loaded') return

  loadState.value.saveState = { type: 'saving' }
  try {
    const keys = loadState.value.entries.map(e => e.key.trim())
    if (keys.some(k => k === '')) throw new Error('Entry key must not be empty.')
    if (new Set(keys).size !== keys.length) throw new Error('Entry keys must be unique.')
    await api.setConfig(toDto(loadState.value.entries))
    loadState.value.saveState = { type: 'saved' }
  } catch (e) {
    loadState.value.saveState = { type: 'saveError', message: (e as Error).message }
  }
}

onMounted(load)
</script>

<template>
  <section class="space-y-4">
    <header class="flex items-center justify-between">
      <h2 class="text-lg font-semibold">Configuration</h2>
      <div class="flex gap-2">
        <button class="btn-secondary" :disabled="loadState.type === 'loading'" @click="load">Reload</button>
        <button v-if="loadState.type === 'loaded'" class="btn-primary"
          :disabled="loadState.saveState.type === 'saving'"
          @click="save">{{ loadState.saveState.type === 'saving' ? 'Saving…' : 'Save' }}
        </button>
      </div>
    </header>

    <ErrorMessage v-if="loadState.type === 'loadError'" :message="loadState.message" />
    <ErrorMessage v-if="loadState.type === 'loaded' && loadState.saveState.type === 'saveError'" :message="loadState.saveState.message" />
    <p v-if="loadState.type === 'loaded' && loadState.saveState.type === 'saved'" class="rounded bg-green-100 px-3 py-2 text-sm text-green-800">Configuration saved.</p>
    <p v-if="loadState.type === 'loading'" class="text-sm text-gray-500">Loading…</p>

    <ul v-if="loadState.type === 'loaded'" class="space-y-3">
      <li v-for="(entry, index) in loadState.entries" :key="index" class="rounded border border-gray-500 p-3">
        <div class="flex flex-wrap items-center gap-2">
          <input v-model="entry.key" placeholder="Key" class="input flex-1 text-orange-500" />
          <select v-model="entry.kind" class="input w-56">
            <option v-for="kind in kinds" :key="kind" :value="kind">{{ configKindLabels[kind] }}</option>
          </select>
          <button class="btn-danger" title="Remove" @click="removeEntry(index)">Delete</button>
        </div>

        <div class="mt-2 space-y-2">
          <LabeledInput v-if="entry.kind === 'text'" label="Content">
            <input v-model="entry.text" placeholder="Value" class="input w-full" />
          </LabeledInput>

          <template v-if="entry.kind === 'credential'">
            <LabeledInput label="User name">
              <input v-model="entry.userName" placeholder="User name" class="input w-full" />
            </LabeledInput>
            <LabeledInput label="Password">
              <input v-model="entry.password" type="password" placeholder="Password" class="input w-full" />
            </LabeledInput>
          </template>

          <template v-if="entry.kind === 'file' || entry.kind === 'certificate'">
            <div class="flex items-center gap-2">
              <LabeledInput label="File">
                <input type="file" class="text-sm" @change="onFile(entry, $event)" />
              </LabeledInput>
            </div>
            <LabeledInput v-if="entry.kind === 'certificate'" label="Password">
              <input v-model="entry.password"
                type="password"
                placeholder="Certificate password"
                class="input w-full"
              />
            </LabeledInput>
          </template>
        </div>
      </li>
    </ul>

    <button v-if="loadState.type === 'loaded'" class="btn-secondary" @click="addEntry">+ Add entry</button>
  </section>
</template>
