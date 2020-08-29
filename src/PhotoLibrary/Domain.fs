module PhotoLibrary.Domain

type Base64EncodedImage = Base64EncodedImage of string

type TeacherPhoto = {
    ShortName: string
    Data: Base64EncodedImage
}

type SokratesId = SokratesId of string

type StudentPhoto = {
    StudentId: SokratesId
    Data: Base64EncodedImage
}
