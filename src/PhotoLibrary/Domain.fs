module PhotoLibrary.Domain

type Base64EncodedJpgImage = Base64EncodedJpgImage of string

type Photo = {
    PersonId: string
    Data: Base64EncodedJpgImage
}

type PhotoType =
    | TeacherPhoto of teacherId: string
    | StudentPhoto of studentId: string

type PhotoUpdate =
    | AddPhoto of PhotoType * SixLabors.ImageSharp.Image
    | RemovePhoto of PhotoType
