<script setup lang="ts">
import { computed } from 'vue'
import type { StudentDto, StudentIdentifierDto, TeacherDto } from './DataSync'
import ErrorWithRetry from './ErrorWithRetry.vue'
import * as _ from 'lodash-es'

const props = defineProps<{
  isSyncingStudentData: boolean
  hasSyncingStudentDataFailed: boolean
  syncedStudentData: StudentDto[] | undefined
  isSyncingTeacherData: boolean
  hasSyncingTeacherDataFailed: boolean
  syncedTeacherData: TeacherDto[] | undefined
}>()

type StudentDataSyncError = {
  type: 'no-match'
  studentName: StudentIdentifierDto
} | {
  type: 'mail-address-not-found'
  studentName: {
    lastName: string
    firstName: string
    className: string
  }
} | {
  type: 'address-not-found'
  studentName: {
    lastName: string
    firstName: string
    className: string
  }
}
const studentDataSyncErrors = computed(() => {
  if (props.syncedStudentData === undefined) return []

  return props.syncedStudentData.flatMap(v => [
    ...(v.type === 'no-match' ? [ <StudentDataSyncError>{ type: 'no-match', studentName: v.name } ] : []),
    ...(v.type === 'exact-match' && v.data.mailAddress === undefined ? [ <StudentDataSyncError>{ type: 'mail-address-not-found', studentName: { lastName: v.data.lastName, firstName: v.data.firstName, className: v.data.className } } ] : []),
    ...(v.type === 'exact-match' && v.data.address === undefined ? [ <StudentDataSyncError>{ type: 'address-not-found', studentName: { lastName: v.data.lastName, firstName: v.data.firstName, className: v.data.className } } ] : []),
  ])
})

type TeacherDataSyncError = {
  type: 'no-match'
  teacherName: string
}
const teacherDataSyncErrors = computed(() => {
  if (props.syncedTeacherData === undefined) return []

  return props.syncedTeacherData.flatMap(v => [
    ...(v.type === 'no-match' ? [ <TeacherDataSyncError>{ type: 'no-match', teacherName: v.name } ] : []),
  ])
})

defineEmits<{
  syncStudentData: []
  syncTeacherData: []
}>()
</script>

<template>
  <div class="flex flex-col gap-4">
    <h2 class="text-xl text-blue-800">Schüler</h2>
    <div v-if="isSyncingStudentData">Schülerdaten werden abgeglichen...</div>
    <ErrorWithRetry v-else-if="hasSyncingStudentDataFailed" type="inline" class="self-start" @retry="$emit('syncStudentData')">Fehler beim Laden der Schülerdaten.</ErrorWithRetry>
    <div v-else-if="syncedStudentData !== undefined">
      <div v-if="syncedStudentData.every(v => v.type === 'exact-match')">{{ syncedStudentData.length }} Schülerdaten wurden erfolgreich abgeglichen.</div>
      <div v-else class="flex flex-col gap-2">
        <span>Folgende Schülerdaten konnten nicht abgeglichen werden:</span>
        <ul class="list-disc ml-4">
          <li v-for="error in studentDataSyncErrors" :key="JSON.stringify(error)" :class="{ 'text-red-800': error.type === 'no-match', 'text-yellow-500': error.type === 'mail-address-not-found' || error.type === 'address-not-found' }">
            <span v-if="error.type === 'no-match'">{{ 'fullName' in error.studentName ? error.studentName.fullName : `${error.studentName.lastName} ${error.studentName.firstName}` }} ({{ error.studentName.className }})</span>
            <span v-else-if="error.type === 'mail-address-not-found'">{{ error.studentName.lastName }} {{ error.studentName.firstName }} ({{ error.studentName.className }}) - Mailadresse nicht gefunden</span>
            <span v-else-if="error.type === 'address-not-found'">{{ error.studentName.lastName }} {{ error.studentName.firstName }} ({{ error.studentName.className }}) - Wohnadresse nicht gefunden</span>
          </li>
        </ul>
      </div>
    </div>
  </div>

  <div class="flex flex-col gap-4">
    <h2 class="text-xl text-blue-800">Lehrer</h2>
    <div v-if="isSyncingTeacherData">Lehrerdaten werden abgeglichen...</div>
    <ErrorWithRetry v-else-if="hasSyncingTeacherDataFailed" type="inline" class="self-start" @retry="$emit('syncTeacherData')">Fehler beim Laden der Lehrerdaten.</ErrorWithRetry>
    <div v-else-if="syncedTeacherData !== undefined">
      <div v-if="syncedTeacherData.every(v => v.type === 'exact-match')">{{ syncedTeacherData.length }} Lehrerdaten wurden erfolgreich abgeglichen.</div>
      <div v-else class="flex flex-col gap-2">
        <span>Folgende Lehrerdaten konnten nicht abgeglichen werden:</span>
        <ul class="list-disc ml-4">
          <li v-for="error in teacherDataSyncErrors" :key="error.teacherName" class="text-red-800">
            <span>{{ error.teacherName }}</span>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>