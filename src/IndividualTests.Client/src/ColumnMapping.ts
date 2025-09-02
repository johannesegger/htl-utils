import * as _ from 'lodash-es'
import type { Cell } from './Excel'
import type { StudentIdentifierDto } from './DataSync'

export type ColumnName = string | undefined
export type ColumnMapping = {
  name: 'studentName'
  selectedType: 'separate' | 'combined'
  columnNames: { firstName: ColumnName; lastName: ColumnName, fullName: ColumnName }
} | {
  name: 'className' | 'subject' | 'teacher1' | 'teacher2' | 'date' | 'beginWritten' | 'endWritten' | 'beginOral' | 'endOral' | 'room'
  columnName: ColumnName
}
export type ColumnIdentifier = Exclude<ColumnMapping['name'], 'studentName'> | 'studentFullName' | 'studentFirstName' | 'studentLastName'
export type MappedCell = {
  mappedToColumn: ColumnIdentifier | undefined
  type: 'text'
  value: string | undefined
  text: string
} | {
  mappedToColumn: ColumnIdentifier | undefined
  type: 'date'
  value: Date | undefined
  text: string
} | {
  mappedToColumn: ColumnIdentifier | undefined
  type: 'time'
  value: Date | undefined
  text: string
}
export namespace ColumnMapping {
  export const getTitle = (columnMappingName: ColumnMapping['name']) : string => {
    switch (columnMappingName) {
      case 'studentName': return 'Name des Schülers'
      case 'className': return 'Klasse des Schülers'
      case 'subject': return 'Prüfungsgegenstand'
      case 'teacher1': return 'Prüfer'
      case 'teacher2': return 'Beisitz'
      case 'date': return 'Datum'
      case 'beginWritten': return 'Beginn schriftlich'
      case 'endWritten' : return 'Ende schriftlich'
      case 'beginOral': return 'Beginn mündlich'
      case 'endOral': return 'Ende mündlich'
      case 'room': return 'Raum'
    }
  }
  export const getColumnIdentifier = (columnMappings: ColumnMapping[], columnName: string) : ColumnIdentifier | undefined => {
    for (const columnMapping of columnMappings) {
      switch (columnMapping.name) {
        case 'studentName':
          switch (columnMapping.selectedType) {
            case 'separate':
              if (columnMapping.columnNames.lastName === columnName) return 'studentLastName'
              if (columnMapping.columnNames.firstName === columnName) return 'studentFirstName'
            case 'combined':
              if (columnMapping.columnNames.fullName === columnName) return 'studentFullName'
          }
          break
        case 'className':
        case 'subject':
        case 'teacher1':
        case 'teacher2':
        case 'date':
        case 'beginWritten':
        case 'endWritten':
        case 'beginOral':
        case 'endOral':
        case 'room':
          if (columnMapping.columnName === columnName) return columnMapping.name
          break
      }
    }
    return undefined
  }
  export const clearColumnNamesNotInList = (columnMapping: ColumnMapping, list: string[]) => {
    switch(columnMapping.name) {
      case 'studentName':
        if (columnMapping.columnNames.firstName !== undefined && !list.includes(columnMapping.columnNames.firstName)) columnMapping.columnNames.firstName = undefined
        if (columnMapping.columnNames.lastName !== undefined && !list.includes(columnMapping.columnNames.lastName)) columnMapping.columnNames.lastName = undefined
        if (columnMapping.columnNames.fullName !== undefined && !list.includes(columnMapping.columnNames.fullName)) columnMapping.columnNames.fullName = undefined
        return
      case 'className':
      case 'subject':
      case 'teacher1':
      case 'teacher2':
      case 'date':
      case 'beginWritten':
      case 'endWritten':
      case 'beginOral':
      case 'endOral':
      case 'room':
        if (columnMapping.columnName !== undefined && !list.includes(columnMapping.columnName)) columnMapping.columnName = undefined
        return
    }
  }

  export const init = () : ColumnMapping[] => [
    {
      name: 'studentName',
      selectedType: 'combined',
      columnNames: { firstName: undefined, lastName: undefined, fullName: undefined },
    },
    { name: 'className', columnName: undefined },
    { name: 'subject', columnName: undefined },
    { name: 'teacher1', columnName: undefined },
    { name: 'teacher2', columnName: undefined },
    { name: 'date', columnName: undefined },
    { name: 'beginWritten', columnName: undefined },
    { name: 'endWritten', columnName: undefined },
    { name: 'beginOral', columnName: undefined },
    { name: 'endOral', columnName: undefined },
    { name: 'room', columnName: undefined }
  ]

  export const getColumnValue = (columnIdentifier: ColumnIdentifier | undefined, cell: Cell) : MappedCell => {
    switch (columnIdentifier) {
      case undefined:
        switch (cell.type) {
          case 'empty':
          case 'string': return { mappedToColumn: columnIdentifier, type: 'text', value: cell.value, text: cell.text }
          case 'date': return { mappedToColumn: columnIdentifier, type: 'date', value: cell.value, text: cell.text }
        }
      case 'studentFullName':
      case 'studentLastName':
      case 'studentFirstName':
      case 'className':
      case 'subject':
      case 'teacher1':
      case 'teacher2':
      case 'room':
        switch (cell.type) {
          case 'empty':
          case 'string': return { mappedToColumn: columnIdentifier, type: 'text', value: cell.value, text: cell.text }
          case 'date': return { mappedToColumn: columnIdentifier, type: 'text', value: undefined, text: cell.text }
        }
      case 'date':
        switch (cell.type) {
          case 'empty':
          case 'string': return { mappedToColumn: columnIdentifier, type: 'date', value: undefined, text: cell.text }
          case 'date': return { mappedToColumn: columnIdentifier, type: 'date', value: cell.value, text: cell.text }
        }
      case 'beginWritten':
      case 'endWritten':
      case 'beginOral':
      case 'endOral':
        switch (cell.type) {
          case 'empty':
          case 'string': return { mappedToColumn: columnIdentifier, type: 'time', value: undefined, text: cell.text }
          case 'date': return { mappedToColumn: columnIdentifier, type: 'time', value: cell.value, text: cell.text }
        }
    }
  }

  export const getStudentNames = (columnMappings: ColumnMapping[], columnNames: string[], rows: MappedCell[][]) : StudentIdentifierDto[] => {
    const studentNameColumnNames = columnMappings.flatMap(v => v.name === 'studentName' ? [ v.columnNames ] : [])[0]
    const studentClassNameColumnName = columnMappings.flatMap(v => v.name === 'className' ? [ v.columnName ] : [])[0]

    if (studentNameColumnNames === undefined || studentClassNameColumnName === undefined) {
      return []
    }
    const studentColumnIndices = {
      fullName: studentNameColumnNames.fullName === undefined ? -1 : columnNames.indexOf(studentNameColumnNames.fullName),
      lastName: studentNameColumnNames.lastName === undefined ? -1 : columnNames.indexOf(studentNameColumnNames.lastName),
      firstName: studentNameColumnNames.firstName === undefined ? -1 : columnNames.indexOf(studentNameColumnNames.firstName),
      className: columnNames.indexOf(studentClassNameColumnName)
    }

    return _.chain(rows)
      .map(row => {
        if (studentNameColumnNames.fullName !== undefined) {
          return { className: row[studentColumnIndices.className]?.text, fullName: row[studentColumnIndices.fullName]?.text }
        }
        else {
          return {
            className: row[studentColumnIndices.className]?.text,
            lastName: row[studentColumnIndices.lastName]?.text,
            firstName: row[studentColumnIndices.firstName]?.text,
          }
        }
      })
      .uniqWith(_.isEqual)
      .value()
  }

  export const getTeacherNames = (columnMappings: ColumnMapping[], columnNames: string[], rows: MappedCell[][]) => {
    const teacherNameColumnNames = (() => {
      const result = [] as string[]
      for (const columnMapping of columnMappings) {
        switch (columnMapping.name) {
          case 'teacher1':
          case 'teacher2':
            if (columnMapping.columnName !== undefined) {
              result.push(columnMapping.columnName)
            }
          default: break
        }
      }
      return result
    })()

    const teacherNameColumnIndices = teacherNameColumnNames.map(v => columnNames.indexOf(v))

    return _.chain(rows)
      .flatMap(row => teacherNameColumnIndices.map(idx => row[idx].text))
      .uniq()
      .value()
  }
}
