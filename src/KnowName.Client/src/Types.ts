import * as DataTransfer from './DataTransfer.User'

export type SelectedPersonGroup = {
  displayName: string
  persons: DataTransfer.Person[]
  selectablePersons: number
  isSelected: boolean
}