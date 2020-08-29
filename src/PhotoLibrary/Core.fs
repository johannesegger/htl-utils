module PhotoLibrary.Core

open PhotoLibrary.Domain
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open System
open System.IO
open System.Text.RegularExpressions

module private TeacherPhoto =
    let tryGetShortName (path: string) =
        let fileName = Path.GetFileNameWithoutExtension path
        if Regex.IsMatch(fileName, @"^[A-Z]{4}$")
        then Some fileName
        else None

    let tryParse readFn file =
        tryGetShortName file
        |> Option.map (fun shortName ->
            {
                ShortName = shortName
                Data = readFn file |> Convert.ToBase64String |> Base64EncodedImage
            }
        )

module private StudentPhoto =
    let tryGetStudentId (path: string) =
        let fileName = Path.GetFileNameWithoutExtension path
        if Regex.IsMatch(fileName, @"^\d+$")
        then Some (SokratesId fileName)
        else None

    let tryParse readFn file =
        tryGetStudentId file
        |> Option.map (fun studentId ->
            {
                StudentId = studentId
                Data = readFn file |> Convert.ToBase64String |> Base64EncodedImage
            }
        )

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

let getTeachersWithPhotos () =
    let baseDir = Environment.getEnvVarOrFail "PHOTO_LIBRARY_TEACHER_PHOTOS_DIRECTORY"
    Directory.GetFiles baseDir
    |> Seq.choose TeacherPhoto.tryGetShortName
    |> Seq.toList

let getStudentsWithPhotos () =
    let baseDir = Environment.getEnvVarOrFail "PHOTO_LIBRARY_STUDENT_PHOTOS_DIRECTORY"
    Directory.GetFiles baseDir
    |> Seq.choose StudentPhoto.tryGetStudentId
    |> Seq.toList

let private tryGetFile baseDir fileName =
    Directory.GetFiles(baseDir, sprintf "%s.*" fileName, EnumerationOptions(MatchCasing = MatchCasing.CaseInsensitive))
    |> Array.tryHead

let getTeacherPhotos size =
    let baseDir = Environment.getEnvVarOrFail "PHOTO_LIBRARY_TEACHER_PHOTOS_DIRECTORY"
    Directory.GetFiles baseDir
    |> Seq.choose (TeacherPhoto.tryGetShortName >> Option.bind (tryGetFile baseDir) >> Option.bind (TeacherPhoto.tryParse (resize size)))
    |> Seq.toList

let tryGetTeacherPhoto shortName size =
    let baseDir = Environment.getEnvVarOrFail "PHOTO_LIBRARY_TEACHER_PHOTOS_DIRECTORY"
    tryGetFile baseDir shortName
    |> Option.bind (TeacherPhoto.tryParse (resize size))

let tryGetStudentPhoto studentId size =
    let baseDir = Environment.getEnvVarOrFail "PHOTO_LIBRARY_STUDENT_PHOTOS_DIRECTORY"
    tryGetFile baseDir studentId
    |> Option.bind (StudentPhoto.tryParse (resize size))
