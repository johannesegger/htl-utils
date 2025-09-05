<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ColumnMapping, MappedCell } from './ColumnMapping'
import * as _ from 'lodash-es'

const props = defineProps<{
  columnNames: string[]
  rows: MappedCell[][]
  columnMappings: ColumnMapping[]
}>()

const teacherDataSortColumns = ref<{ name: string, direction: 'asc' | 'desc' }[]>([])
const setTeacherSortColumn = (columnName: string) => {
  const column = teacherDataSortColumns.value.find(v => v.name === columnName)
  if (column === undefined) {
    teacherDataSortColumns.value.push({ name: columnName, direction: 'asc' })
  }
  else {
    switch (column.direction) {
      case 'asc': column.direction = 'desc'; break
      case 'desc': teacherDataSortColumns.value.splice(teacherDataSortColumns.value.indexOf(column), 1); break
    }
  }
}

const sortTeacherRows = (columnNames: string[], rows: MappedCell[][]) => {
  const columnTextFns = teacherDataSortColumns.value.map(v => ((row: MappedCell[]) => row[columnNames.indexOf(v.name)].value))
  const sortOrders = teacherDataSortColumns.value.map(v => v.direction)
  return _.orderBy(rows, columnTextFns, sortOrders)
}

const teacherTables = computed(() => {
  const teacherColumnNames = props.columnMappings.flatMap(v => {
    switch (v.name) {
      case 'teacher1':
      case 'teacher2':
        return [ v.columnName ].filter(v => v !== undefined)
      default: return []
    }
  })

  const teacherColumnIndices = teacherColumnNames.map(v => props.columnNames.indexOf(v))

  const teachers = _.chain(props.rows)
    .flatMap(row => teacherColumnIndices.map(idx => row[idx].text))
    .uniq()
    .sort()
    .value()

  return teachers.map(teacher => {
      const rows = props.rows.filter(row => teacherColumnIndices.map(idx => row[idx].text).includes(teacher))
      const sortedRows = sortTeacherRows(props.columnNames, rows)
      return {
        teacher: teacher,
        rows: sortedRows
      }
    })
})
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex items-center gap-2">
      <span class="shrink-0">Springe zu:</span>
      <div class="flex flex-wrap gap-2">
        <a v-for="teacher in teacherTables.map(v => v.teacher)" :key="teacher" :href="`#${teacher}`" class="text-blue-800 hover:underline">{{ teacher }}</a>
      </div>
    </div>
    <div class="flex flex-col gap-4">
      <div v-for="table in teacherTables" :key="table.teacher" class="flex flex-col gap-2">
        <h2 :id="table.teacher" class="text-xl text-blue-800">{{ table.teacher }}</h2>
        <table>
          <thead class="bg-blue-500/25">
            <tr>
              <th v-for="columnName in columnNames" :key="columnName" class="px-2 py-1 border border-gray-300 cursor-pointer" @click="setTeacherSortColumn(columnName)">
                <div class="flex gap-2">
                  <span>{{ columnName }}</span>
                  <span v-if="teacherDataSortColumns.some(v => v.name === columnName && v.direction === 'asc')">↑</span>
                  <span v-else-if="teacherDataSortColumns.some(v => v.name === columnName && v.direction === 'desc')">↓</span>
                </div>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in table.rows" :key="JSON.stringify(row)">
              <td v-for="col in row" :key="col.text" class="px-2 py-1 border border-gray-300" :class="{ 'bg-yellow-300/50': col.mappedToColumn !== undefined && col.value === undefined, 'bg-green-500/50': col.mappedToColumn !== undefined && col.value !== undefined }">{{ col.text }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
