<script setup lang="ts">
import { ColumnMapping, type MappedCell } from './ColumnMapping'
import ColumnMappingForm from './ColumnMappingForm.vue'

defineProps<{
  columnNames: string[]
  rows: MappedCell[][]
}>()
defineModel<ColumnMapping[]>('column-mappings')
</script>

<template>
  <div class="flex flex-col gap-2">
    <span class="input-label">Spaltenzuordnung</span>
    <ColumnMappingForm :column-names="columnNames" v-model="columnMappings" />
  </div>

  <table class="border border-gray-300">
    <thead class="bg-blue-500/25">
      <tr>
        <th v-for="columnName in columnNames" :key="columnName" class="px-2 py-1 border border-gray-300">{{ columnName }}</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="row in rows" :key="JSON.stringify(row)">
        <td v-for="col in row" :key="col.text" class="px-2 py-1 border border-gray-300" :class="{ 'bg-yellow-300/50': col.mappedToColumn !== undefined && col.value === undefined, 'bg-green-500/50': col.mappedToColumn !== undefined && col.value !== undefined }">{{ col.text }}</td>
      </tr>
    </tbody>
  </table>
</template>