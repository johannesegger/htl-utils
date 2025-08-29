import type { Cell } from "./Excel"

export type ColumnName = string | undefined
export type ColumnMapping = {
  name: 'studentName'
  selectedType: 'separate' | 'combined'
  columnNames: { firstName: ColumnName; lastName: ColumnName, fullName: ColumnName }
} | {
  name: 'className' | 'subject' | 'teacher1' | 'teacher2' | 'date' | 'beginWritten' | 'endWritten' | 'beginOral' | 'endOral' | 'room'
  columnName: ColumnName
}
export type MappedCell = {
  isMapped: boolean
  type: 'text'
  value: string | undefined
  text: string
} | {
  isMapped: boolean
  type: 'date'
  value: Date | undefined
  text: string
} | {
  isMapped: boolean
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
  export const getByColumnName = (columnMappings: ColumnMapping[], columnName: string) : ColumnMapping | undefined => {
    for (const columnMapping of columnMappings) {
      switch (columnMapping.name) {
        case 'studentName':
          const columnNames = (() => {
            switch (columnMapping.selectedType) {
              case 'separate': return [ columnMapping.columnNames.lastName, columnMapping.columnNames.firstName ]
              case 'combined': return [ columnMapping.columnNames.fullName ]
            }
          })()
          if (columnNames.includes(columnName))
            return columnMapping
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
          if (columnMapping.columnName === columnName) return columnMapping
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

  export const getColumnValue = (columnMapping: ColumnMapping | undefined, cell: Cell) : MappedCell => {
    switch (columnMapping?.name) {
      case undefined:
        switch (cell.type) {
          case 'empty':
          case 'string': return { isMapped: false, type: 'text', value: cell.value, text: cell.text }
          case 'date': return { isMapped: false, type: 'date', value: cell.value, text: cell.text }
        }
      case 'studentName':
      case 'className':
      case 'subject':
      case 'teacher1':
      case 'teacher2':
      case 'room':
        switch (cell.type) {
          case 'empty':
          case 'string': return { isMapped: true, type: 'text', value: cell.value, text: cell.text }
          case 'date': return { isMapped: true, type: 'text', value: undefined, text: cell.text }
        }
      case 'date':
        switch (cell.type) {
          case 'empty':
          case 'string': return { isMapped: true, type: 'date', value: undefined, text: cell.text }
          case 'date': return { isMapped: true, type: 'date', value: cell.value, text: cell.text }
        }
      case 'beginWritten':
      case 'endWritten':
      case 'beginOral':
      case 'endOral':
        switch (cell.type) {
          case 'empty':
          case 'string': return { isMapped: true, type: 'time', value: undefined, text: cell.text }
          case 'date': return { isMapped: true, type: 'time', value: cell.value, text: cell.text }
        }
    }
  }
}
