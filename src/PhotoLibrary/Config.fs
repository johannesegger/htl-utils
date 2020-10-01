namespace PhotoLibrary.Configuration

type Config = {
    TeacherPhotosDirectory: string
    StudentPhotosDirectory: string
}
module Config =
    let fromEnvironment () =
        {
            TeacherPhotosDirectory = Environment.getEnvVarOrFail "PHOTO_LIBRARY_TEACHER_PHOTOS_DIRECTORY"
            StudentPhotosDirectory = Environment.getEnvVarOrFail "PHOTO_LIBRARY_STUDENT_PHOTOS_DIRECTORY"
        }
