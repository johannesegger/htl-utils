export type CreateGuestAccountsRequest = {
  group: string
  count: number
  wlanOnly: boolean
  notes: string | undefined
}

export type CreateGuestAccountsValidationError = 'InvalidGroupName' | 'InvalidSize'
export type CreatedGuestAccount = {
  account: {
    userName: string
    password: string
    notes: string | undefined
  }
  errors: string[]
}
export type CreatedGuestAccountGroup = {
  group: string
  accounts: CreatedGuestAccount[]
  pdf: string // base64 encoded
}
export type CreateGuestAccountsResponse =
  { type: 'not-authorized' } |
  { type: 'validation-error', error: CreateGuestAccountsValidationError } |
  { type: 'accounts-created', result: CreatedGuestAccountGroup } |
  { type: 'other-error' }

export type ExistingAccountGroup = {
  group: string
  accounts: {
    name: string
    createdAt: string
    wlanOnly: boolean
    notes: string | undefined
  }[]
}
