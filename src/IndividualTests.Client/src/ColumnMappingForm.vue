<script setup lang="ts">
import { ColumnMapping } from './ColumnMapping'
import SelectInput from './SelectInput.vue'

defineProps<{
  columnNames: string[]
}>()

const model= defineModel<ColumnMapping[]>()
</script>

<template>
  <template v-for="columnMapping in model" :key="columnMapping.name">
    <template v-if="columnMapping.name === 'studentName'">
      <div class="flex items-center gap-2">
        <label class="flex gap-2">
          <input type="radio" value="combined" v-model="columnMapping.selectedType" />
          <span>Vor- und Nachname des Schülers</span>
        </label>
        <span>→</span>
        <SelectInput empty-text="-- Nicht vorhanden --" :options="columnNames.map(v => ({ value: v, text: v }))" v-model="columnMapping.columnNames.fullName" />
      </div>
      <div class="flex items-center gap-4">
        <div class="flex items-center gap-2">
          <label class="flex gap-2">
            <input type="radio" value="separate" v-model="columnMapping.selectedType" />
            <span>Nachname des Schülers</span>
          </label>
          <span>→</span>
          <SelectInput empty-text="-- Nicht vorhanden --" :options="columnNames.map(v => ({ value: v, text: v }))" v-model="columnMapping.columnNames.lastName" />
        </div>
        <div class="flex items-center gap-2">
          <span>Vorname des Schülers</span>
          <span>→</span>
          <SelectInput empty-text="-- Nicht vorhanden --" :options="columnNames.map(v => ({ value: v, text: v }))" v-model="columnMapping.columnNames.firstName" />
        </div>
      </div>
    </template>
    <template v-else>
      <div class="flex items-center gap-2">
        <span>{{ ColumnMapping.getTitle(columnMapping.name) }}</span>
        <span>→</span>
          <SelectInput empty-text="-- Nicht vorhanden --" :options="columnNames.map(v => ({ value: v, text: v }))" v-model="columnMapping.columnName" />
      </div>
    </template>
  </template>
</template>