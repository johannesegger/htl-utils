module PhotoLibrary.Core

open PhotoLibrary.Configuration
open PhotoLibrary.Domain
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open System
open System.IO

let private resizePhoto size (path: string) =
    use image = Image.Load path
    image.Mutate(fun x ->
        let resizeOptions =
            ResizeOptions(
                Size = size,
                Mode = ResizeMode.Crop,
                CenterCoordinates = Nullable<_>(PointF( 0.f, 0.4f ))
            )
        x.Resize resizeOptions |> ignore
    )
    use target = new MemoryStream()
    image.SaveAsJpeg target
    target.Seek(0L, SeekOrigin.Begin) |> ignore
    target.ToArray()

let private resize (width, height) =
    match width, height with
    | Some width, Some height -> resizePhoto (Size (width, height))
    | Some width, None -> resizePhoto (Size (width, 0))
    | None, Some height -> resizePhoto (Size (0, height))
    | None, None -> File.ReadAllBytes

let tryLoad (content: byte[]) =
    try
        Some (Image.Load(content))
    with _ -> None

let private getPhotoFiles dir =
    Directory.GetFiles (dir,  "*.jpg", EnumerationOptions(MatchCasing = MatchCasing.CaseInsensitive))
    |> Array.toList

let getTeachersWithPhotos = reader {
    let! config = Reader.environment
    return getPhotoFiles config.TeacherPhotosDirectory
        |> List.map Path.GetFileNameWithoutExtension
}

let getStudentsWithPhotos = reader {
    let! config = Reader.environment
    return getPhotoFiles config.StudentPhotosDirectory
        |> List.map Path.GetFileNameWithoutExtension
}

let private tryGetPhotoFile baseDir fileName =
    Directory.GetFiles(baseDir, $"%s{fileName}.jpg", EnumerationOptions(MatchCasing = MatchCasing.CaseInsensitive))
    |> Array.tryHead

let private getPhoto size (filePath: string) =
    {
        PersonId = Path.GetFileNameWithoutExtension filePath
        Data = resize size filePath |> Convert.ToBase64String |> Base64EncodedJpgImage
    }

let private tryGetPhoto baseDir fileName size =
    tryGetPhotoFile baseDir fileName
    |> Option.map (getPhoto size)

let tryGetTeacherPhoto teacherId size = reader {
    let! config = Reader.environment
    return tryGetPhoto config.TeacherPhotosDirectory teacherId size
}

let tryGetStudentPhoto studentId size = reader {
    let! config = Reader.environment
    return tryGetPhoto config.StudentPhotosDirectory studentId size
}

let getPhotos baseDir size =
    getPhotoFiles baseDir
    |> List.map (getPhoto size)

let getTeacherPhotos size = reader {
    let! config = Reader.environment
    return getPhotos config.TeacherPhotosDirectory size
}

let private saveTeacherPhoto name (image: Image) = reader {
    let! config = Reader.environment
    let path = Path.Combine(config.TeacherPhotosDirectory, $"%s{name}.jpg")
    image.SaveAsJpeg(path)
}

let private saveStudentPhoto name (image: Image) = reader {
    let! config = Reader.environment
    let path = Path.Combine(config.StudentPhotosDirectory, $"%s{name}.jpg")
    image.SaveAsJpeg(path)
}

let private removeTeacherPhoto name = reader {
    let! config = Reader.environment
    tryGetPhotoFile config.TeacherPhotosDirectory name
    |> Option.iter File.Delete
}

let private removeStudentPhoto name = reader {
    let! config = Reader.environment
    tryGetPhotoFile config.StudentPhotosDirectory name
    |> Option.iter File.Delete
}

let update = function
    | AddPhoto (TeacherPhoto name, image) ->
        saveTeacherPhoto name image
    | AddPhoto (StudentPhoto name, image) ->
        saveStudentPhoto name image
    | RemovePhoto (TeacherPhoto name) ->
        removeTeacherPhoto name
    | RemovePhoto (StudentPhoto name) ->
        removeStudentPhoto name
let updates = List.map update >> Reader.sequence >> Reader.ignore
