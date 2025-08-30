<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ColumnMapping, type MappedCell } from './ColumnMapping'
import { uiFetch } from './UIFetch'
import ErrorWithRetry from './ErrorWithRetry.vue'

const props = defineProps<{
  columnNames: string[]
  rows: MappedCell[][]
  columnMappings: ColumnMapping[]
}>()

const studentNames = computed(() => {
  return ColumnMapping.getStudentNames(props.columnMappings, props.columnNames, props.rows)
})
const teacherNames = computed(() => {
  return ColumnMapping.getTeacherNames(props.columnMappings, props.columnNames, props.rows)
})

type StudentDto = {
  type: 'exact-match'
  name: { fullName: string } | { lastName: string, firstName: string}
  data: {
    sokratesId: string
    lastName: string
    firstName: string
    className: string
    mailAddress: string
    gender: 'm' | 'f'
    address: {
      country: string
      zip: string
      city: string
      street: string
    }
  }
} | {
  type: 'no-match'
  name: { fullName: string } | { lastName: string, firstName: string}
}
type TeacherDto = {
  type: 'exact-match'
  name: string
  data: {
    shortName: string
    lastName: string
    firstName: string
    mailAddress: string
  }
} | {
  type: 'no-match'
  name: string
}

const isSyncingStudentData = ref(false)
const hasSyncingStudentDataFailed = ref(false)
const syncedStudentData = ref<StudentDto[]>()

const isSyncingTeacherData = ref(false)
const hasSyncingTeacherDataFailed = ref(false)
const syncedTeacherData = ref<TeacherDto[]>()

const syncStudentData = async () => {
  const result = await uiFetch(isSyncingStudentData, hasSyncingStudentDataFailed, '/api/sync/students', {
    method: 'QUERY',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(studentNames.value)
  })
  if (result.succeeded) {
    syncedStudentData.value = await result.response.json()
  }
  else {
    syncedStudentData.value = undefined
  }
}
const syncTeacherData = async () => {
  const result = await uiFetch(isSyncingTeacherData, hasSyncingTeacherDataFailed, '/api/sync/teachers', {
    method: 'QUERY',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(teacherNames.value)
  })
  if (result.succeeded) {
    syncedTeacherData.value = await result.response.json()
  }
  else {
    syncedTeacherData.value = undefined
  }
}

watch(props, async _props => {
  await syncStudentData()
  await syncTeacherData()
}, { immediate: true, deep: true })
</script>

<template>
  <ErrorWithRetry v-if="hasSyncingStudentDataFailed" type="inline" class="self-start" @retry="syncStudentData">Fehler beim Laden der Schülerdaten.</ErrorWithRetry>
  <ErrorWithRetry v-if="hasSyncingTeacherDataFailed" type="inline" class="self-start" @retry="syncTeacherData">Fehler beim Laden der Lehrerdaten.</ErrorWithRetry>
</template>