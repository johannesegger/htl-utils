export type Person = {
  displayName: string
  imageUrl: string | null
}

export type PersonGroup = {
  displayName: string
  persons: Person[]
}
