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