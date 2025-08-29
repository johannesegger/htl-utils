<script setup lang="ts">
import * as XLSX from 'xlsx'
import FileInput from './FileInput.vue'
import { computed, reactive, ref, watch } from 'vue'
import * as _ from 'lodash-es'
import { ColumnMapping, type MappedCell } from './ColumnMapping'
import { Cell } from './Excel'
import ColumnMappingForm from './ColumnMappingForm.vue'

// type StudentData = {
//   sokratesId: string
//   firstName: string
//   lastName: string
//   className: string
//   mailAddress: string
//   address: {
//     country: string
//     zip: string
//     city: string
//     street: string
//   }
// }
// const studentData = ref<StudentData[]>()
// const loadStudentData = async () => {
  
// }
// loadStudentData()

const dataFile = ref<File>()
const doc = ref<XLSX.WorkBook>()
watch(dataFile, async () => {
  if (dataFile.value === undefined) return

  const content = await dataFile.value.bytes()
  doc.value = XLSX.read(content, { cellDates: true })
  sheetName.value = sheetName.value !== undefined && doc.value.SheetNames.includes(sheetName.value) ? sheetName.value : doc.value.SheetNames[0];
})
const sheetName = ref<string>()
const sheet = computed(() => {
  if (doc.value === undefined || sheetName.value === undefined) return undefined

  return doc.value.Sheets[sheetName.value]
})

const rawTableData = ref<{columnNames: string[], rows: Cell[][]}>()
watch(sheet, sheet => {
  if (sheet === undefined) return undefined

  const data = XLSX.utils.sheet_to_json<(number|string|undefined)[]>(sheet, { blankrows: false, skipHidden: true, header: 1, raw: true })
  const columnNames = data[0].map(v => `${v || ''}`)
  rawTableData.value = {
    columnNames: columnNames,
    rows: data.slice(1).map((row, rowIndex) => {
      return Array.from({length: Math.max(data[0].length, row.length)}, (_item, columnIndex) => {
        const cellAddress = XLSX.utils.encode_cell({ r: rowIndex + 1, c: columnIndex })
        return Cell.parse(sheet[cellAddress]);
      })
    })
  }
}, { deep: true })

const storedColumnMappings = <ColumnMapping[]>JSON.parse(<string>localStorage.getItem('columnMappings-v1'))
const columnMappings = reactive(storedColumnMappings || ColumnMapping.init())
watch(columnMappings, columnMappings => {
  localStorage.setItem('columnMappings-v1', JSON.stringify(columnMappings))
}, { deep: true })

const view = ref<'list' | 'teacher'>('list')
const teacherViewError = computed(() => {
  const hasUnmapped = columnMappings.some(v => {
    switch (v.name) {
      case 'teacher1':
      case 'teacher2':
        return v.columnName === undefined
      default: return false
    }
  })
  return hasUnmapped ? 'Bitte die Spalten "Prüfer" und "Beisitz" zuordnen, um auf die Lehreransicht umzuschalten.' : undefined
})

const tableData = ref<{columnNames: string[], rows: MappedCell[][]}>()
watch([rawTableData, columnMappings], ([rawTableData, columnMappings]) => {
  if (rawTableData === undefined) return undefined

  for (const columnMapping of columnMappings) {
    ColumnMapping.clearColumnNamesNotInList(columnMapping, rawTableData.columnNames)
  }
  
  tableData.value = {
    columnNames: rawTableData.columnNames,
    rows: rawTableData.rows.map(row => {
      return row.map((value, columnIndex) => {
        const columnMapping = ColumnMapping.getByColumnName(columnMappings, rawTableData.columnNames[columnIndex])
        return ColumnMapping.getColumnValue(columnMapping, value)
      })
    })
  }
}, { deep: true })

const teacherDataSortColumns = ref<{ name: string, direction: 'asc' | 'desc' }[]>([])
const setTeacherSortColumn = (teacher: string, columnName: string) => {
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

const teacherData = computed(() => {
  if (tableData.value === undefined) return

  const currentTableData = tableData.value

  const teacherColumnNames = columnMappings.flatMap(v => {
    switch (v.name) {
      case 'teacher1':
      case 'teacher2':
        return [ v.columnName ].filter(v => v !== undefined)
      default: return []
    }
  })

  const teacherColumnIndices = teacherColumnNames.map(v => currentTableData.columnNames.indexOf(v))

  const teachers = _.chain(currentTableData.rows)
    .flatMap(row => teacherColumnIndices.map(idx => row[idx].text))
    .uniq()
    .sort()
    .value()

  return {
    columnNames: currentTableData.columnNames,
    tables: teachers.map(teacher => {
      const rows = currentTableData.rows.filter(row => teacherColumnIndices.map(idx => row[idx].text).includes(teacher))
      const sortedRows = sortTeacherRows(currentTableData.columnNames, rows)
      return {
        teacher: teacher,
        rows: sortedRows
      }
    })
  }
})
</script>

<template>
  <div class="m-4 flex flex-col gap-4">
    <h1 class="text-3xl text-blue-800">Prüfungseinteilung</h1>
    <FileInput title="Excel-Datei" :file-types="['.xlsx']" v-model="dataFile" class="self-start" />
    <div v-if="doc !== undefined" class="flex flex-col">
      <span class="input-label">Blatt</span>
      <div class="flex flex-wrap gap-2">
        <button v-for="sheet in doc.SheetNames" :key="sheet" class="btn" :class="{ 'bg-green-500 text-white': sheetName === sheet }" @click="sheetName = sheet">{{ sheet }}</button>
      </div>
    </div>
    <div v-if="tableData?.columnNames !== undefined" class="flex flex-col gap-2">
      <span class="input-label">Spaltenzuordnung</span>
      <ColumnMappingForm :column-names="tableData.columnNames" v-model="columnMappings" />
    </div>
    <div v-if="tableData !== undefined" class="flex flex-col gap-2">
      <span class="input-label">Ansicht</span>
      <div class="flex gap-2">
        <button class="btn" :class="{ 'bg-green-500 text-white': view === 'list' }" @click="view = 'list'">Liste</button>
        <button class="btn" :disabled="teacherViewError !== undefined" :title="teacherViewError" :class="{ 'bg-green-500 text-white': view === 'teacher' }" @click="view = 'teacher'">Lehrer</button>
      </div>
    </div>
    <table v-if="view === 'list' && tableData?.columnNames !== undefined" class="border border-gray-300">
      <thead class="bg-blue-500/25">
        <tr>
          <th v-for="columnName in tableData.columnNames" :key="columnName" class="px-2 py-1 border border-gray-300">{{ columnName }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in tableData.rows" :key="JSON.stringify(row)">
          <td v-for="col in row" :key="col.text" class="px-2 py-1 border border-gray-300" :class="{ 'bg-red-500/50': col.isMapped && col.value === undefined, 'bg-green-500/50': col.isMapped && col.value !== undefined }">{{ col.text }}</td>
        </tr>
      </tbody>
    </table>
    <div v-else-if="view === 'teacher' && teacherData !== undefined" class="flex flex-col gap-4">
      <div v-for="table in teacherData.tables" :key="table.teacher" class="flex flex-col gap-2">
        <h2 class="text-xl text-blue-800">{{ table.teacher }}</h2>
        <table>
          <thead class="bg-blue-500/25">
          <tr>
            <th v-for="columnName in teacherData.columnNames" :key="columnName" class="px-2 py-1 border border-gray-300 cursor-pointer" @click="setTeacherSortColumn(table.teacher, columnName)">
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
          <td v-for="col in row" :key="col.text" class="px-2 py-1 border border-gray-300" :class="{ 'bg-red-500/50': col.isMapped && col.value === undefined, 'bg-green-500/50': col.isMapped && col.value !== undefined }">{{ col.text }}</td>
          </tr>
        </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

