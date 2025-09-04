<script setup lang="ts">
import * as XLSX from 'xlsx'
import FileInput from './FileInput.vue'
import { computed, reactive, ref, watch } from 'vue'
import * as _ from 'lodash-es'
import { ColumnMapping, type MappedCell } from './ColumnMapping'
import { Cell } from './Excel'
import ColumnMappingForm from './ColumnMappingForm.vue'
import ListView from './ListView.vue'
import TeacherView from './TeacherView.vue'
import DataSyncView from './DataSyncView.vue'
import { syncStudentData, syncTeacherData, type StudentDto, type TeacherDto } from './DataSync'
import { TestData } from './TestData'
import StudentLetters from './StudentLetters.vue'
import TeacherLetters from './TeacherLetters.vue'

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

const storedColumnMappings = <ColumnMapping[]>JSON.parse(<string>localStorage.getItem('columnMappings-v2'))
const columnMappings = reactive(storedColumnMappings || ColumnMapping.init())
watch(columnMappings, columnMappings => {
  localStorage.setItem('columnMappings-v2', JSON.stringify(columnMappings))
}, { deep: true })

const view = ref<'test-list' | 'teacher-lists' | 'data-sync' | 'student-letters' | 'teacher-letters'>('test-list')

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
        const columnIdentifier = ColumnMapping.getColumnIdentifier(columnMappings, rawTableData.columnNames[columnIndex])
        return ColumnMapping.getColumnValue(columnIdentifier, value)
      })
    })
  }
}, { deep: true })

const teacherViewError = computed(() => {
  const hasUnmappedColumns = columnMappings.some(v => {
    switch (v.name) {
      case 'teacher1':
      case 'teacher2':
        return v.columnName === undefined
      default: return false
    }
  })
  return hasUnmappedColumns ? `Bitte die Spalten "${ColumnMapping.getTitle('teacher1')}" und "${ColumnMapping.getTitle('teacher2')}" zuordnen, um auf die Lehreransicht umzuschalten.` : undefined
})

const dataSyncError = computed(() => {
  const hasUnmappedColumns = columnMappings.some(v => {
    switch (v.name) {
      case 'studentName':
        switch (v.selectedType) {
          case 'separate': return v.columnNames.firstName === undefined || v.columnNames.lastName === undefined
          case 'combined': return v.columnNames.fullName === undefined
        }
      case 'className':
        return v.columnName === undefined
      default: return false
    }
  })
  return hasUnmappedColumns ? `Bitte die Spalten "${ColumnMapping.getTitle('studentName')}" und "${ColumnMapping.getTitle('className')}" zuordnen, um den Datenabgleich zu starten.` : undefined
})

const studentNames = computed(() => {
  if (tableData.value === undefined) return undefined
  return ColumnMapping.getStudentNames(columnMappings, tableData.value.columnNames, tableData.value.rows)
})

const teacherNames = computed(() => {
  if (tableData.value === undefined) return undefined
  return ColumnMapping.getTeacherNames(columnMappings, tableData.value.columnNames, tableData.value.rows)
})

const isSyncingStudentData = ref(false)
const hasSyncingStudentDataFailed = ref(false)
const syncedStudentData = ref<StudentDto[]>()
let syncStudentDataAbortController = new AbortController()
const resyncStudentData = async () => {
  if (studentNames.value === undefined) return

  syncStudentDataAbortController.abort()
  syncStudentDataAbortController = new AbortController()
  syncedStudentData.value = await syncStudentData(studentNames.value, isSyncingStudentData, hasSyncingStudentDataFailed, syncStudentDataAbortController.signal)
}
watch(studentNames, async () => {
  await resyncStudentData()
}, { immediate: true })

const isSyncingTeacherData = ref(false)
const hasSyncingTeacherDataFailed = ref(false)
const syncedTeacherData = ref<TeacherDto[]>()
let syncTeacherDataAbortController = new AbortController()
const resyncTeacherData = async () => {
  if (teacherNames.value === undefined) {
    return
  }
  syncTeacherDataAbortController.abort()
  syncTeacherDataAbortController = new AbortController()
  syncedTeacherData.value = await syncTeacherData(teacherNames.value, isSyncingTeacherData, hasSyncingTeacherDataFailed, syncTeacherDataAbortController.signal)
}
watch(teacherNames, async () => {
  await resyncTeacherData()
}, { immediate: true })

const testData = computed<TestData[] | undefined>(() => {
  return TestData.create(tableData.value, syncedStudentData.value, syncedTeacherData.value)
})

const studentLettersError = computed(() => {
  if (testData.value === undefined) {
    return 'Bitte zuerst die Spalten zuweisen und den Datenabgleich starten, um die Schülerbriefe zu erzeugen.'
  }
  return undefined
})
const teacherLettersError = computed(() => {
  if (testData.value === undefined) {
    return 'Bitte zuerst die Spalten zuweisen und den Datenabgleich starten, um die Lehrerbriefe zu erzeugen.'
  }
  return undefined
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
    <div v-if="tableData !== undefined" class="flex flex-col gap-2">
      <span class="input-label">Spaltenzuordnung</span>
      <ColumnMappingForm :column-names="tableData.columnNames" v-model="columnMappings" />
    </div>
    <div v-if="tableData !== undefined" class="flex flex-col gap-2">
      <span class="input-label">Ansicht</span>
      <div class="flex gap-2">
        <button class="btn" :class="{ 'bg-green-500 text-white': view === 'test-list' }" @click="view = 'test-list'">Prüfungsliste</button>
        <button class="btn" :disabled="teacherViewError !== undefined" :title="teacherViewError" :class="{ 'bg-green-500 text-white': view === 'teacher-lists' }" @click="view = 'teacher-lists'">Lehrerlisten</button>
        <button class="btn" :disabled="dataSyncError !== undefined" :title="dataSyncError" :class="{ 'bg-green-500 text-white': view === 'data-sync' }" @click="view = 'data-sync'">Datenabgleich mit Sokrates</button>
        <button class="btn" :disabled="studentLettersError !== undefined" :title="studentLettersError" :class="{ 'bg-green-500 text-white': view === 'student-letters' }" @click="view = 'student-letters'">Schülerbriefe erstellen</button>
        <button class="btn" :disabled="teacherLettersError !== undefined" :title="teacherLettersError" :class="{ 'bg-green-500 text-white': view === 'teacher-letters' }" @click="view = 'teacher-letters'">Lehrerbriefe erstellen</button>
      </div>
    </div>
    <ListView v-if="view === 'test-list' && tableData !== undefined"
      :column-names="tableData.columnNames"
      :rows="tableData.rows" />
    <TeacherView v-else-if="view === 'teacher-lists' && tableData !== undefined"
      :column-names="tableData.columnNames"
      :rows="tableData.rows"
      :column-mappings="columnMappings" />
    <DataSyncView v-if="view === 'data-sync' && tableData !== undefined"
      :is-syncing-student-data="isSyncingStudentData"
      :has-syncing-student-data-failed="hasSyncingStudentDataFailed"
      :synced-student-data="syncedStudentData"
      @sync-student-data="resyncStudentData"
      :is-syncing-teacher-data="isSyncingTeacherData"
      :has-syncing-teacher-data-failed="hasSyncingTeacherDataFailed"
      :synced-teacher-data="syncedTeacherData"
      @sync-teacher-data="resyncTeacherData" />
    <StudentLetters v-if="view === 'student-letters' && testData !== undefined"
      :tests="testData" />
    <TeacherLetters v-if="view === 'teacher-letters' && testData !== undefined"
      :tests="testData" />
  </div>
</template>

