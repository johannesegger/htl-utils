import * as DataTransfer from './DataTransfer'

export type SelectedPersonGroup = {
  displayName: string
  persons: DataTransfer.Person[]
  selectablePersons: number
  isSelected: boolean
}