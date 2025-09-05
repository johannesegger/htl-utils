import type { Ref } from "vue"
import { uiFetch } from "./UIFetch"

export type StudentIdentifierDto =
    { className: string, fullName: string } |
    { className: string, lastName: string, firstName: string }

export type StudentDto = {
  type: 'exact-match'
  name: StudentIdentifierDto
  data: {
    sokratesId: string
    lastName: string
    firstName: string
    className: string
    mailAddress: string | undefined
    gender: 'm' | 'f'
    address: {
      country: string
      zip: string
      city: string
      street: string
    } | undefined
  }
} | {
  type: 'no-match'
  name: StudentIdentifierDto
}

export type StudentDataSyncError = {
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
export const getStudentDataSyncErrors = (syncedStudentData: StudentDto[]) => {
  return syncedStudentData.flatMap(v => [
    ...(v.type === 'no-match' ? [ <StudentDataSyncError>{ type: 'no-match', studentName: v.name } ] : []),
    ...(v.type === 'exact-match' && v.data.mailAddress === undefined ? [ <StudentDataSyncError>{ type: 'mail-address-not-found', studentName: { lastName: v.data.lastName, firstName: v.data.firstName, className: v.data.className } } ] : []),
    ...(v.type === 'exact-match' && v.data.address === undefined ? [ <StudentDataSyncError>{ type: 'address-not-found', studentName: { lastName: v.data.lastName, firstName: v.data.firstName, className: v.data.className } } ] : []),
  ])
}

export type TeacherDto = {
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

export type TeacherDataSyncError = {
  type: 'no-match'
  teacherName: string
}
export const getTeacherDataSyncErrors = (syncedTeacherData: TeacherDto[]) => {
  return syncedTeacherData.flatMap(v => [
    ...(v.type === 'no-match' ? [ <TeacherDataSyncError>{ type: 'no-match', teacherName: v.name } ] : []),
  ])
}

export const syncStudentData = async (
  studentNames: StudentIdentifierDto[],
  isSyncing: Ref<boolean>,
  hasSyncingFailed: Ref<boolean>,
  signal: AbortSignal) => {
  const result = await uiFetch(isSyncing, hasSyncingFailed, '/api/sync/students', {
    method: 'QUERY',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(studentNames),
    signal,
  })
  if (result.succeeded) {
    return await result.response.json() as StudentDto[]
  }
  return undefined
}

export const syncTeacherData = async (
  teacherNames: string[],
  isSyncing: Ref<boolean>,
  hasSyncingFailed: Ref<boolean>,
  signal: AbortSignal) => {
  const result = await uiFetch(isSyncing, hasSyncingFailed, '/api/sync/teachers', {
    method: 'QUERY',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(teacherNames),
    signal,
  })
  if (result.succeeded) {
    return await result.response.json() as TeacherDto[]
  }
  return undefined
}