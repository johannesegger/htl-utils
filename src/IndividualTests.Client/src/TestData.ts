import * as _ from "lodash-es"
import type { MappedCell } from "./ColumnMapping"
import type { StudentDto, StudentIdentifierDto, TeacherDto } from "./DataSync"

export type TimeOfDay = {
  hours: number
}
export type TestPart = {
   type: 'exact-time-span',
   begin: TimeOfDay,
   end: TimeOfDay,
} | {
  type: 'exact-time',
  time: TimeOfDay,
} | {
  type: 'begin-time',
  time: TimeOfDay,
} | {
  type: 'afterwards'
}
export type TestData = {
  testType: 'Wiederholungsprüfung' | 'NOSTPrüfung' | 'Übertrittsprüfung' | 'Semesterprüfung'
  student: {
    lastName: string | undefined
    firstName: string | undefined
    className: string | undefined
    mailAddress: string | undefined
    gender: 'm' | 'f' | undefined
    address: {
      country: string
      zip: string
      city: string
      street: string
    } | undefined
  }
  subject: string | undefined
  teacher1: {
    shortName: string | undefined
    lastName: string | undefined
    firstName: string | undefined
    mailAddress: string | undefined
  }
  teacher2: TestData['teacher1'] | undefined
  date: Date | undefined
  partWritten: TestPart | undefined
  partOral: TestPart | undefined
  room: string | undefined
  additionalData: {
    columnName: string
    value: string
  }[]
}

export namespace TestData {
  export const createTestPart = (begin: MappedCell | undefined, end: MappedCell | undefined) : TestPart | undefined => {
    if (begin === undefined || end === undefined) {
      return undefined
    }

    if (begin.value instanceof Date && end.value instanceof Date) {
      return {
        type: 'exact-time-span',
        begin: { hours: begin.value.getHours() + begin.value.getMinutes() / 60 },
        end: { hours: end.value.getHours() + end.value.getMinutes() / 60 }
      }
    }
    // TODO match other types, not necessary at the moment
    // if (begin.value instanceof Date) {
    //   return {
    //     type: 'begin-time',
    //     time: { hours: begin.value.getHours() + begin.value.getMinutes() / 60 }
    //   }
    // }
    return undefined
  }
  export const create = (
    tableData: { columnNames: string[], rows: MappedCell[][] } | undefined,
    studentData: StudentDto[] | undefined,
    teacherData: TeacherDto[] | undefined) => {
      
    if (tableData === undefined) {
      return []
    }

    return tableData.rows.map((row) : TestData => {
      const className = row.find(v => v.mappedToColumn === 'className')?.text

      const fullName = row.find(v => v.mappedToColumn === 'studentFullName')?.text
      const lastName = row.find(v => v.mappedToColumn === 'studentLastName')?.text
      const firstName = row.find(v => v.mappedToColumn === 'studentFirstName')?.text
      let studentIdentifier : StudentIdentifierDto | undefined
      if (fullName !== undefined) {
        studentIdentifier = { className: className || '', fullName: fullName }
      }
      else if (lastName !== undefined && firstName !== undefined) {
        studentIdentifier = { className: className || '', lastName: lastName, firstName: firstName }
      }
      const student = studentData?.flatMap(v => v.type === 'exact-match' && _.isEqual(v.name, studentIdentifier) ? [ v.data ] : [])[0]

      const teacher1Identifier = row.find(v => v.mappedToColumn === 'teacher1')?.value as (string | undefined)
      const teacher1 = teacherData?.flatMap(v => v.type === 'exact-match' && _.isEqual(v.name, teacher1Identifier) ? [ v.data ] : [])[0]
      const teacher2Identifier = row.find(v => v.mappedToColumn === 'teacher2')?.value as (string | undefined)
      const teacher2 = teacherData?.flatMap(v => v.type === 'exact-match' && _.isEqual(v.name, teacher2Identifier) ? [ v.data ] : [])[0]
      return {
        testType: 'Wiederholungsprüfung',
        student: {
          lastName: student?.lastName || lastName || fullName?.split(' ')[0],
          firstName: student?.firstName || firstName || fullName?.split(' ')[1],
          className: student?.className || className,
          mailAddress: student?.mailAddress,
          gender: student?.gender,
          address: student?.address,
        },
        subject: row.find(v => v.mappedToColumn === 'subject')?.value as (string | undefined),
        teacher1: {
          shortName: teacher1?.shortName || teacher1Identifier,
          lastName: teacher1?.lastName,
          firstName: teacher1?.firstName,
          mailAddress: teacher1?.mailAddress,
        },
        teacher2: {
          shortName: teacher2?.shortName || teacher2Identifier,
          lastName: teacher2?.lastName,
          firstName: teacher2?.firstName,
          mailAddress: teacher2?.mailAddress,
        },
        date: row.find(v => v.mappedToColumn === 'date')?.value as (Date | undefined),
        partWritten: createTestPart(row.find(v => v.mappedToColumn === 'beginWritten'), row.find(v => v.mappedToColumn === 'endWritten')),
        partOral: createTestPart(row.find(v => v.mappedToColumn === 'beginOral'), row.find(v => v.mappedToColumn === 'endOral')),
        room: row.find(v => v.mappedToColumn === 'room')?.value as string | undefined,
        additionalData: row.flatMap((v, idx) => v.mappedToColumn === undefined ? [{ columnName: tableData.columnNames[idx], value: v.text }] : [])
      }
    })
  }
}
