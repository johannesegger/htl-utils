export type Person = {
  displayName: string
  imageUrl: string | null
}

export type PersonGroup = {
  displayName: string
  persons: Person[]
}

export type Settings = {
  personGroups: PersonGroup[]
}

export type UploadPhotosResult = {
  newTeacherPhotos: string[]
  removedTeacherPhotos: string[]
  newStudentPhotos: string[]
  removedStudentPhotos: string[]
}
