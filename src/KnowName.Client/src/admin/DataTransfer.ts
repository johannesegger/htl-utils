export type Person = {
  displayName: string
  imageUrl: string | null
}

export type PersonGroup = {
  displayName: string
  persons: Person[]
}

export type UploadPhotosResult = {
  updatedTeacherPhotos: string[]
  updatedStudentPhotos: string[]
}

export type ExistingSettings = {
  sokrates: {
    webServiceUrl: string
    schoolId: string
    userName: string
    password: string
    clientCertificate: {
      subject: string
      issuer: string
      validFrom: string
      validUntil: string
    }
  } | null
}

export type NewSettings = {
  sokrates: {
    webServiceUrl: string
    schoolId: string
    userName: string
    password: string
    clientCertificate: string | undefined // base64
    clientCertificatePassphrase: string
  }
}

export type SaveSettingsError =
  'invalid-sokrates-certificate' |
  'incomplete-config'
